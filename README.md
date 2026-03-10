# Framenion

Framenion is a fast, lightweight Warframe companion desktop app built with Avalonia UI.

## Features

- Equipment browser with:
  - Category filters (Warframes, Primary, Secondary, Melee, Archwing categories, Companions, Vehicles, Amps)
  - Search by item name
  - Mastery status highlighting
  - Ingredient ownership checks
  - Craftability hints when ingredient requirements are met
- Void Fissure panel with:
  - Normal / Steel Path filtering
  - Tier, mission type, faction, location, and countdown
  - Auto-refresh and live timer updates
  - Desktop toast notifications for newly opened matching fissures
- In-app inventory refresh flow using warframe-api-helper (download prompted on first use)

## To-do

- [ ] Warframe Market integration: Add live price data (buy/sell)

- [ ] Relic viewer: Add a dedicated relic browser with drop tables, rarity tiers, and quick search/filter by relic era and reward.

- [ ] Reward overlay when opening relics:  Show an in-game overlay with item details (owned count, ducat value, market price, mastery/craft relevance) during relic opening.

- [ ] Ensure cross-platform compatibility for Windows and Linux.

## Disclaimer

Use any third-party tooling at your own risk.
Framenion itself does not interact with the Warframe game process.

This project is not affiliated with Digital Extremes.
Warframe and all related trademarks are the property of their respective owners.

## Credits

- browse.wf team for infrastructure and data resources: https://browse.wf/ and https://github.com/calamity-inc/warframe-public-export-plus
- warframe-api-helper for inventory export tooling: https://github.com/Sainan/warframe-api-helper