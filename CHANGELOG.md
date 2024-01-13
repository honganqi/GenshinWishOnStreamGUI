# Changelog
All notable changes to this project will be documented in
this file.

## 1.2 - 2023/01/14
### Changed
* Adapted to the changes to the script where array of strings
were changed to array of character-element pairs
### Fixed
* Fixed issue where user is unable to reconnect Twitch account
if the token is expired.

## 1.1 - 2023/01/02
### Added
* A log file is now created when the app crashes
* Added "Genshin_Wish.html" to the list of files to check
### Changed
* Upon first launch, the app now looks for the script files in
the same folder
### Fixed
* Fixed the "Connect to Twitch" button not working on initial
launch
* Fixed the issue where the Channel Point Rewards were not
being listed after connecting to Twitch
* Fixed numerous issues with adding and deleting columns of star
values in the Characters page
* Deleting the last remaining column of star value is no longer
possible
* The app now remembers your settings when it is updated to a 
newer version
* Fixed a logic error in checking the saved Wisher path

## 1.0 - 2022/12/31
* initial release