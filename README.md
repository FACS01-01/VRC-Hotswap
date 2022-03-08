# VRC Hotswap
[![](https://img.shields.io/github/downloads/FACS01-01/VRC-Hotswap/total.svg)](https://github.com/FACS01-01/VRC-Hotswap/releases)
[![](https://img.shields.io/github/v/release/FACS01-01/VRC-Hotswap)](https://github.com/FACS01-01/VRC-Hotswap/releases/latest)
[![](https://img.shields.io/github/downloads/FACS01-01/VRC-Hotswap/latest/total.svg)](https://github.com/FACS01-01/VRC-Hotswap/releases/latest)

Script for performing a Hotswap when uploading avatars to VRChat

## How to use
0. Install the required Unity version (+Android Build Support) and create a Unity project.
1. Import the VRC Hotswap Unity Package.
2. Install a VRChat SDK manually on step 0, or install it now with the Menu button "VRC Hotswap/Get Latest VRC SDK"
3. Create a dummy avatar with the Menu button "VRC Hotswap/Spawn Dummy Avi".
4. If you want to overwrite an existing avatar, or upload a Quest version to your already uploaded PC avatar, attach the corresponding Avatar ID into the Dummy Avi's Pipeline Manager component.
5. If you are uploading a brand new avatar, make sure there is no ID attached to the Dummy Avi's Pipeline Manager component.
6. Press the "Build & Publish" button on the VRChat SDK window, to start the upload process.
7. When you get to the "Avatar Name & Description" screen, the Menu button "VRC Hotswap/Hotswap" will be unlocked. Press the button.
8. Select the .vrca file of the avatar you want to Hotswap.
9. Wait until the Console in Unity says "HOTSWAP SUCCESSFUL".
10. You can finish the uploading process hitting the "Upload" button.

## Thanks to / Using
My fork of [AssetsTools.NET](https://github.com/FACS01-01/AssetsTools.NET), embedded into "VRC Hotswap Compressor.exe", used for recompression with progress bar.
