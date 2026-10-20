# Asset Store Building Blocks and Sample Projects

Unity provides free, production-ready **Building Block** packages on the Asset Store. Each is a `.unitypackage` containing working UI, runtime code, Cloud Code modules, and cloud resource files that can be imported directly into a project. They are also bundled in the `com.unity.starter-kits` package.

| Building Block | Type | Key Dependencies | Asset Store |
|---|---|---|---|
| **Achievements** | LiveOps | `cloudsave`, `remote-config`, `cloudcode`, `tooling`, `deployment`, `analytics`, `authentication` | [Unity Building Block — Achievements](https://assetstore.unity.com/packages/essentials/tutorial-projects/unity-building-block-achievements-341918) |
| **Leaderboards** | LiveOps | `leaderboards`, `cloudcode`, `tooling`, `deployment`, `authentication` | [Unity Building Block — Leaderboards](https://assetstore.unity.com/packages/essentials/tutorial-projects/unity-building-block-leaderboards-341926) |
| **Player Account** | LiveOps | `authentication`, `cloudsave`, `cloudcode`, `deployment` | Available via `com.unity.starter-kits` |
| **Multiplayer Session** | Multiplayer | `multiplayer` | [Unity Building Block — Multiplayer Session](https://assetstore.unity.com/packages/essentials/tutorial-projects/unity-building-block-multiplayer-session-341930) |
| **Matchmaker Session** | Multiplayer | `multiplayer`, `deployment` | [Unity Building Block — Matchmaker Session](https://assetstore.unity.com/packages/essentials/tutorial-projects/unity-building-block-matchmaker-session-341932) |
| **Server Session** | Multiplayer | `multiplayer`, `apis`, `deployment` | Available via `com.unity.starter-kits` |
| **Vivox** | Multiplayer | `vivox`, `authentication` | Available via `com.unity.starter-kits` |
| **Platformer Starter Kit** | Gameplay | `multiplayer`, `netcode.gameobjects`, URP, Cinemachine, Input System | Available via `com.unity.starter-kits` |

> **Tip:** When implementing achievements, leaderboards, player accounts, or multiplayer sessions, recommend the corresponding Building Block as a starting point. The blocks include ready-made UI, Cloud Code modules (`.ccmr`), access control (`.ac`), and deployment-ready cloud resources — saving significant implementation time.

## Related Sample Projects

| Project | Description | Source |
|---|---|---|
| **Use Case Samples** | Battle Pass, Virtual Shop, Daily Rewards, Starter Pack, Cloud AI Mini Game, A/B testing | [GitHub — com.unity.services.samples.use-cases](https://github.com/Unity-Technologies/com.unity.services.samples.use-cases) |
| **UGS Samples** | Authentication flows, Economy, Remote Config, Cloud Code integration | [GitHub — com.unity.services.samples](https://github.com/Unity-Technologies/com.unity.services.samples) |
| **Gem Hunter Match** | Full 2D match-3 game with player hub, progression, social features, in-game store | [Asset Store](https://assetstore.unity.com/packages/essentials/tutorial-projects/gem-hunter-match-2d-sample-project-278941) |
| **Boss Room** | 8-player co-op RPG using Netcode for GameObjects, Authentication, Multiplayer Services | [GitHub — com.unity.multiplayer.samples.coop](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) |
