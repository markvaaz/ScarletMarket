# ScarletMarket

**ScarletMarket** is a V Rising mod that adds a comprehensive player trading system. Create your own shops, set up automated NPC traders, and build a thriving marketplace economy with other players on your server. 

---

## Support & Donations

<a href="https://www.patreon.com/bePatron?u=30093731" data-patreon-widget-type="become-patron-button"><img height='36' style='border:0px;height:36px;' src='https://i.imgur.com/o12xEqi.png' alt='Become a Patron' /></a>  <a href='https://ko-fi.com/F2F21EWEM7' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' alt='Buy Me a Coffee at ko-fi.com' /></a>

---

## How It Works

ScarletMarket transforms designated plot areas into player-owned shops. When you claim a plot, an NPC trader appears that you can stock with items for sale. Other players can browse and purchase items from your shop while you're online or offline. All transactions are handled automatically, with payments going directly to your storage chest.

**Shop States:**
- **Closed:** You can add/remove items and set prices
- **Open:** Other players can buy items from your shop

## Features

* **Player-owned shops** with NPC traders that persist when you're offline
* **Plot-based system** with designated market areas
* **Automated trading** - players can buy items even when shop owners are offline
* **Flexible pricing** - set any item as payment for your goods
* **Shop customization** - rename your shop and manage inventory
* **Admin tools** for server management and marketplace control
* **Visual indicators** showing shop status (open/closed)
* **Storage integration** - sold items' payments go to your private storage

## Commands

### Player Commands

* `.market claim` - Set up your own shop in a market plot
* `.market unclaim` - Delete your shop permanently (Must be empty)
* `.market open` - Open your shop for business
* `.market close` - Close your shop for editing
* `.market addcost <itemName> <amount>` - Set the price for an pending item in your shop (max amount 4000)
* `.market rename "<shopName>"` - Change your shop's name (supports international characters)
* `.market getowner` - Check who owns the shop in the plot you're standing in
* `.market search buy <itemName>` - Search for shops selling a specific item
* `.market search sell <itemName>` - Search for shops that want to buy a specific item

### Admin Commands  

**Plot Management:**
* `.market create plot` - Create a new market plot at your location
* `.market forcecreate plot` - Force create a plot (ignores plot radius overlap restrictions)
* `.market select` - Select a plot for moving (must be standing inside the plot)
* `.market deselect` - Clear plot selection
* `.market place` - Move the selected plot to your current location
* `.market move` - Interactive plot moving with mouse aim, rotate with `R`, place with `Left Click`
* `.market move <x> <y> <z>` - Move selected plot to specific coordinates
* `.market forcemove` - Interactive plot moving (ignores plot radius overlap restrictions)
* `.market rotate` - Rotate the plot you're standing in
* `.market remove plot` - Remove an empty market plot
* `.market forceremove plot` - Forcefully remove a plot (deletes shop and all items inside)

**Shop Management:**
* `.market remove shop` - Remove a shop from a plot (must be empty)
* `.market forceremove shop` - Forcefully remove a shop and its plot (deletes all items inside)
* `.market forcerename "<name>"` - Admin rename any shop

**Access Control:**
* `.market claimaccess` - Gain access to view any shop's contents (view only, cannot add/remove items). While active, the shop owner cannot access their shop
* `.market revokeaccess` - Return shop access to original owner

**Visualization:**
* `.market showradius` - Show all plot boundaries
* `.market hideradius` - Hide all plot boundaries

**Maintenance:**
* `.market clear emptyplots` - Remove all empty plots
* `.market clear emptyshops` - Remove all empty shops
* `.market getinactive <days>` - List shops inactive for X days
* `.market iwanttoclearinactiveshops <days>` - Remove shops inactive for X days (deletes all items inside)
* `.market iwanttoremoveeverything` - **DANGER:** Remove all market entities (deletes all items inside)

### Shop Management

1. **Find a market plot** (designated trading areas)
2. **Claim it** with `.market claim`
3. **Add items** by placing them in your shop's inventory
4. **Set prices** with `.market addcost <item> <amount>`
5. **Open shop** with `.market open` or use the **Take All** button
6. **Profit!** Payments automatically go to your storage

### Interface Controls

When managing your shop inventory, you can use the built-in interface buttons:

* **Take All** Button - Automatically opens your shop if all items have prices set (won't open if any item lacks a price)
* **Sort** Button - Instantly closes your shop for editing at any time

These interface controls provide a quick alternative to using `.market open` and `.market close` commands!

### Pricing System

You can set any item as payment for your goods, creating flexible trading opportunities between players.

**Important:** 
* When setting items with special attributes (like legendary weapons with extra stats) as payment, you cannot specify the exact attributes you want to receive. If you set a legendary weapon as payment for your item, you'll get whatever legendary weapon with whatever attributes the buyer has available. For example, if you want an "Apocalypse" as payment, you'll receive whichever Apocalypse the buyer has, regardless of its specific attribute bonuses.
* Buyers cannot choose which specific item from their inventory will be used for payment. The system automatically selects which item to trade. For example, if a shop wants an "Apocalypse" as payment and the buyer has two Apocalypse swords in their inventory, the system will automatically choose one of them - the buyer cannot specify which one to use. Be careful not to accidentally trade valuable items with desired attributes!

**Note:** A future update may include the ability to drag specific items onto shop items to use them as payment, allowing buyers to choose exactly which item from their inventory to trade.

## Installation

### Requirements

This mod requires the following dependencies:

* **[BepInEx](https://wiki.vrisingmods.com/user/bepinex_install.html)**
* **[ScarletCore](https://thunderstore.io/c/v-rising/p/ScarletMods/ScarletCore/)**
* **[VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)**

Make sure BepInEx is installed and loaded **before** installing ScarletMarket.

### Manual Installation

1. Download the latest release of **ScarletMarket**.

2. Extract the contents into your `BepInEx/plugins` folder:

   `<V Rising Server Directory>/BepInEx/plugins/`

   Your folder should now include:

   `BepInEx/plugins/ScarletMarket.dll`

3. Ensure **ScarletCore** and **VampireCommandFramework** are also installed in the `plugins` folder.
4. Start or restart your server.

## Configuration

Server administrators can configure various aspects of ScarletMarket through the mod's settings:

* **Plot claiming requirements** - Set required items/resources to claim plots
* **Shop naming permissions** - Allow or restrict custom shop names
* **Maximum prices** - Limit the maximum amount players can charge
* **Plot management** - Control plot creation and removal
* **Currency items** - Define which items can be used as prices for trading

### Configuration Files

After first launch, the following configuration files will be created:

**Main Configuration:**
* `BepInEx/config/ScarletMarket.cfg` - Main mod settings with default values that you can customize

**Currency Database:**
* `BepInEx/config/ScarletMarket/ItemPrefabNames.json` - Contains the list of items that can be used as currency/prices in the market system

### Currency System Management

The `ItemPrefabNames.json` file controls which items can be used as currency for shop pricing.

* **Add items** to allow them as payment methods
* **Remove items** to restrict them from being used as currency
* **Edit item names** if needed for your server

**Note:** Only items in this file can be used for setting shop prices with `.market addcost`

**⚠️ Warning:** Backup the file before editing. Invalid item IDs can cause errors. To restore defaults, simply delete the file - it will be recreated with the complete item list when the server restarts.

## This project is made possible by the contributions of the following individuals:

- **cheesasaurus, EduardoG, Helskog, Mitch, SirSaia, Odjit** & the [V Rising Mod Community on Discord](https://vrisingmods.com/discord)
