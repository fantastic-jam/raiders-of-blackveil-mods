# ThePit — Terminology

Use these terms consistently in code comments, docs, and conversation.

| Term | Aliases | Description |
|---|---|---|
| **Liberator** | Lobby | The flying boat hub between runs. Players change champion, access chests, buy items, and unlock abilities here. Not a scene we load — players return here after each match via `RPC_Handle_ReturnToLobby`. |
| **Spawn room** | First room | The first room of a ThePit run. Players spawn here, pick their 6 perk chests, then take the floor door to the arena. The `PerkDripController` and door redirect logic runs here. |
| **Arena** | SlashBash room | The Slash & Bash MiniBoss scene repurposed as the PvP arena. No enemies, no traps. Timer, respawn, and kill tracking are active here. Players enter via the floor door from the spawn room. |
| **Floor door** | Door | A door mounted in the floor at the centre of a room. Both the spawn room and the arena have one. `DoorSpawnPoint` is the GameObject name. In the spawn room it leads to the arena; in the arena it's the landmark used for champion positioning at arena start. |
