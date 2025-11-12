# Changelog

## [1.1.0] - 2025-11-12

### New Features
- **Auto-Recovery System**: Shops from older versions are now automatically detected and restored on server restart
- **New Admin Commands**: 
  - `.sm list` - View all active shops on the server
  - `.sm cleanorphans <radius>` - Remove broken shop entities safely
  - `.sm forceremove radius <radius> IAGREE` - Emergency cleanup tool (use with caution)

### Improvements
- **Better Performance**: Reduced server lag when loading many shops
- **Improved Stability**: Shops are now more reliable and less likely to break during server restarts
- **Safer Cleanup**: Better tools for admins to manage problematic shop entities

### Bug Fixes
- Fixed shops sometimes appearing twice in the same location
- Fixed shops occasionally not working properly after server restarts
- Fixed issues with shop cleanup commands not working correctly

## [1.0.2] - 2025-08-06

### Fixed
- Fixed some stuff months ago, I don't remember what :D
- Added Root buff to Trader to prevent movement issues

### Added
- Added reload method in TraderService for dynamic trader data refresh

## [1.0.1] - 2025-08-06

### Added
- Added reload method in TraderService for dynamic trader data refresh
- Added admin reload command for server management
- Added trader blocking mechanism when mod fails to load correctly during server initialization

### Security
- Enhanced ownership validation consistency across all inventory systems
- Improved error handling with proper null checks in trader operations
- Standardized ownership validation across all patches
- Added comprehensive trader registration validation
- Traders are now automatically blocked if mod initialization fails, requiring admin intervention
