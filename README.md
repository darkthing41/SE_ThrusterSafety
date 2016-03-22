# SE_ThrusterSafety
Space Engineers: Disables thusters providing thrust in the same direction as natural gravity.

> This project is configured to be built within a separate IDE to allow for error checking, code completion etc..
> Only the section in `#region CodeEditor` should be copied into the Space Engineers editor. This region has been automatically extracted into the corresponding `txt` file.

##Description
Determines whether a thruster should be enabled by checking it's orientation compared to natural gravity.
+ checks all thrusters on the same grid as the configured Remote Control
+ compares angle to a configured cutoff angle
  - relative to local natural gravity
  - accounting for ship orientation
+ enables thrusters within the safe range
+ disables thusters outside the safe range

##Hardware
| Block(s)      | number        | Configurable  |
| ------------- | ------------- | ------------- |
| Remote Control| single        | by name constant
| Thrusters     | [all]         | no*
*but limited by the algorithm to those on the same grid as the Remote Control

##Configuration
+ `nameRemoteControl`: the name of the Remote Control used to identify the main grid and get local gravity
+ `angleSafetyCutoff`: the angle from gravity above which thrusters will be disabled (in radians)

##Standard Blocks
+ `ValidateBlock()`: check that found blocks are usable
+ Status/Initialise/Validate framework