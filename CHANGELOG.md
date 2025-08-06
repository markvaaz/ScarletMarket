# Changelog

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
