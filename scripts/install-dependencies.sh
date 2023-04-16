#!/bin/bash

sudo apt update

# Avalonia dependencies
sudo apt install libgbm1 libgl1-mesa-dri libegl1-mesa libinput10 libopenal1 -y

# Resona dependencies
sudo apt install uhubctl pulseaudio-module-bluetooth -y

# Start pulseaudio
systemctl --user start pulseaudio && sudo usermod -a -G bluetooth pi
