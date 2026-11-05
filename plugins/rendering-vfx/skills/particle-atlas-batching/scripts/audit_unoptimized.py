import argparse
import collections
import glob
import os
import re
import sys

ATLAS_SHADER = 'ParticleVfx/Atlas AB Add'
DEFAULT_SUPPORTED = [
    'ParticleVfx/Legacy Additive', 'ParticleVfx/Legacy Alpha Blended', 'Legacy Shaders/Particles/Additive',
    'Legacy Shaders/Particles/Alpha Blended', 'Mobile/Particles/Alpha Blended',
]
RENDERERS = ('ParticleSystemRenderer', 'MeshRenderer', 'SpriteRenderer', 'TrailRenderer', 'LineRenderer', 'SkinnedMeshRenderer')
BUILTIN = '0000000000000000f000000000000000'
REFERENCE_EXTENSIONS = ('.prefab', '.unity', '.asset', '.mat', '.controller', '.playable', '.anim')


def read(path):
    with open(path, encoding='utf8', errors='ignore') as handle:
        return handle.read()


def parse_args():
    parser = argparse.ArgumentParser(
        description='List particle VFX prefabs that still draw with non-atlas renderers. Run from the Unity project root.')
    parser.add_argument('folder', help='Folder to audit, e.g. Assets/VFX')
    parser.add_argument('out', help='Output TSV path')
    parser.add_argument('--atlas-shader', default=ATLAS_SHADER, help='Shader name of the atlas material')
    parser.add_argument('--supported', default=','.join(DEFAULT_SUPPORTED),
                        help='Comma-separated shader names the packer accepts')
    parser.add_argument('--referenced', action='store_true',
                        help='Add a usage column: GUID references from assets outside the folder, and Addressables entries')
    return parser.parse_args()


def index_guids():
    guid2path = {}
    for directory, _, files in os.walk('Assets'):
        for name in files:
            if not name.endswith('.meta'):
                continue
            match = re.search(r'guid: (\w+)', read(os.path.join(directory, name))[:4000])
            if match:
                guid2path[match.group(1)] = os.path.join(directory, name[:-5]).replace(os.sep, '/')
    return guid2path


def shader_names(guid2path):
    names = {}
    for guid, path in guid2path.items():
        if path.endswith('.shader'):
            match = re.search(r'Shader\s+"([^"]+)"', read(path)[:3000])
            if match:
                names[guid] = match.group(1)
    return names


def material_shader(guid, guid2path, names):
    if guid == BUILTIN:
        return 'builtin material'
    path = guid2path.get(guid, '')
    if not path:
        return 'missing material'
    if not path.endswith('.mat'):
        return 'not a material'
    match = re.search(r'm_Shader: \{fileID: (-?\d+), guid: (\w+)', read(path))
    if not match:
        return 'no shader'
    if match.group(2) == BUILTIN:
        return 'builtin shader'
    shader_path = guid2path.get(match.group(2), '')
    if shader_path.endswith('.shadergraph'):
        return 'Shader Graphs/' + os.path.basename(shader_path)[:-12]
    return names.get(match.group(2), shader_path or 'unknown shader')


def inspect(path, guid2path, names, atlas_shader):
    text = read(path)
    parts = re.split(r'\n--- !u!\d+ &(-?\d+)[^\n]*\n', text)
    docs = {parts[i]: parts[i + 1] for i in range(1, len(parts) - 1, 2)}
    inactive = {k for k, v in docs.items() if v.startswith('GameObject:') and 'm_IsActive: 0' in v}
    parent = {}
    for key, value in docs.items():
        if value.startswith(('Transform:', 'RectTransform:')):
            go = re.search(r'm_GameObject: \{fileID: (-?\d+)', value)
            father = re.search(r'm_Father: \{fileID: (-?\d+)', value)
            if go:
                parent[go.group(1)] = (key, father.group(1) if father else '0')
    owner = {transform: go for go, (transform, _) in parent.items()}

    def hidden(go):
        while go and go != '0':
            if go in inactive:
                return True
            go = owner.get(parent.get(go, (None, '0'))[1])
        return False

    native, live = collections.Counter(), 0
    for value in docs.values():
        kind = value.split(':', 1)[0]
        if kind not in RENDERERS or re.search(r'\n  m_Enabled: 0', value):
            continue
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)', value)
        if go and hidden(go.group(1)):
            continue
        if kind == 'ParticleSystemRenderer' and re.search(r'\n  m_RenderMode: 5', value):
            continue
        live += 1
        slots = re.search(r'm_Materials:\n((?:  - .*\n)*)', value)
        guids = re.findall(r'guid: (\w+)', slots.group(1)) if slots else []
        if kind == 'ParticleSystemRenderer':
            guids = guids[:1]
        if not guids:
            native['no material'] += 1
        for guid in guids:
            shader = material_shader(guid, guid2path, names)
            if shader != atlas_shader:
                native[shader] += 1
    animator = bool(re.search(r'\n--- !u!(95|111) ', text))
    nested = bool(re.search(r'\n--- !u!1001 ', text))
    return native, live, animator, nested


def usage(folder, prefab_guids):
    referenced = collections.defaultdict(set)
    for directory, _, files in os.walk('Assets'):
        normalized = directory.replace(os.sep, '/')
        for name in files:
            if not name.endswith(REFERENCE_EXTENSIONS):
                continue
            path = normalized + '/' + name
            if path.startswith(folder + '/'):
                continue
            for guid in set(re.findall(r'guid: ([0-9a-f]{32})', read(path))) & prefab_guids:
                referenced[guid].add(path)
    addressable = set()
    for path in glob.glob('Assets/AddressableAssetsData/AssetGroups/*.asset'):
        addressable.update(re.findall(r'm_GUID: (\w+)', read(path)))
    return referenced, addressable


def main():
    args = parse_args()
    folder = args.folder.rstrip('/').replace(os.sep, '/')
    supported = {name.strip() for name in args.supported.split(',') if name.strip()}
    guid2path = index_guids()
    names = shader_names(guid2path)
    rows = []
    for path in sorted(glob.glob(folder + '/**/*.prefab', recursive=True)):
        path = path.replace(os.sep, '/')
        native, live, animator, nested = inspect(path, guid2path, names, args.atlas_shader)
        if not native:
            continue
        guid = re.search(r'guid: (\w+)', read(path + '.meta')).group(1)
        blockers = sorted(k for k in native if k not in supported)
        if animator:
            blockers.append('Animator/Animation')
        if nested:
            blockers.append('nested prefab')
        if blockers:
            status = 'needs review'
        elif live > 2 and sum(native.values()) >= 2:
            status = 'should pack'
        else:
            status = 'optional (little to merge)'
        rows.append([path, guid, status, str(live), '; '.join(f'{k} x{v}' for k, v in native.items()), ', '.join(blockers)])
    header = ['prefab', 'guid', 'status', 'live_renderers', 'non_atlas_renderers', 'blockers']
    if args.referenced:
        referenced, addressable = usage(folder, {row[1] for row in rows})
        header.append('usage')
        for row in rows:
            tags = []
            if row[1] in addressable:
                tags.append('addressable')
            if referenced[row[1]]:
                tags.append(f'{len(referenced[row[1]])} external reference(s)')
            row.append(', '.join(tags) or 'no reference found')
    with open(args.out, 'w', encoding='utf8') as out:
        out.write('\t'.join(header) + '\n')
        for row in rows:
            out.write('\t'.join(row) + '\n')
    print('flagged', len(rows), dict(collections.Counter(row[2] for row in rows)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
