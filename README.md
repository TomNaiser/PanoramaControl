# PanoControl
PanoramaControl is a control software for an automated pan/tilt panorama head.

 It is written in C# (VisualStudio 2017) and its graphical user interface is windows forms based. PanoramaControl is running on Microsoft Windows (tested on Windows 7,8 and 10). I am running it from a small intel atom based netbook. Via a wireless connection (e.g. Bluetooth) position and camera(shutter)-commands are sent to the automated panorama head.  The automated panorama head is controlled by a microcontroller - currently an Arduino Mega is in use. The arduino sketch PanoDuinoControl (another project of mine that can be found here on Github) handles the communication with PanoControl, and operates the RC-servos, position encoders and camera shutters. Cameras require a remote shutter  release which can be triggered by the microcontroller.


Features:

-software is tailored for ease of use, with a focus on the acquisition of gigapixel panoramas comprising thousands of single shots 

-configuration of stitching parameters for various cameras and camera/lens-combinations (e.g. Sony A7 with various lenses, Lumix FZ200, Lumix FZ28)

-adjustable timing, overlap parameters

-configuration (new Cameras etc.) can be set in PanoControlConfig.xml

-enter start- and end-positions for panorama acquisition

-start/stop/pause acquisition 

-timelapse mode

-recording of exact shoot positions (based on position encoder readings) for import professional stitiching software

-wireless communication with microcontroller-based panorama head 

-can be extended to various acquisition modes (default: flat panorama for large gigapixel panoramas) . I didn't care much about spherical 
 panoramas (not very well implemented yet), but a proper implementation shouldn't be too difficult.

