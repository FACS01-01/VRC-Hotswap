# VRC-Hotswap
Script for performing a Hotswap when uploading avatars to VRChat

## How to use
1. Import the VRC Hotswap Unity Package.
2. Create a dummy avatar with the Menu button "VRC Hotswap/Spawn Dummy Avi".
3. If you want to overwrite an existing avatar, or upload a Quest version to your already uploaded PC avatar, attach the corresponding Avatar ID into the Dummy Avi's Pipeline Manager component.
4. If you are uploading a brand new avatar, make sure there is no ID attached to the Dummy Avi's Pipeline Manager component.
5. Press the "Build & Publish" button on the VRChat SDK window, to start the upload process.
6. When you get to the "Avatar Name & Description" screen, the Menu button "VRC Hotswap/Hotswap" will be unlocked. Press the button.
7. Select the .vrca file of the avatar you want to Hotswap.
8. Wait until the Console in Unity says "HOTSWAP SUCCESSFUL".
9. You can finish the uploading process hitting the "Upload" button.

## Thanks to
[A.R.E.S](https://github.com/cassell1337/A.R.E.S) for the files "HOTSWAP.exe" and "AssetsTools.NET.dll", used for fast recompression.
