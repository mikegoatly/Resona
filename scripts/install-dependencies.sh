#!/bin/bash

sudo apt update

# Avalonia dependencies
sudo apt install libgbm1 libgl1-mesa-dri libegl1-mesa libinput10 libopenal1 -y

# Resona dependencies
sudo apt install uhubctl pulseaudio-module-bluetooth bluetooth -y

# Spotify Connect (experimental): librespot, taken from the raspotify package (armhf/arm64 builds).
# Resona runs librespot itself as a user service (see configure-spotify.sh), so raspotify's own
# system service is disabled and masked.
sudo apt install curl gpg -y
curl -sSL https://dtcooper.github.io/raspotify/key.asc | sudo tee /usr/share/keyrings/raspotify_key.asc > /dev/null
sudo chmod 644 /usr/share/keyrings/raspotify_key.asc
echo 'deb [signed-by=/usr/share/keyrings/raspotify_key.asc] https://dtcooper.github.io/raspotify raspotify main' | sudo tee /etc/apt/sources.list.d/raspotify.list > /dev/null
sudo apt update
sudo apt install raspotify -y
sudo systemctl disable --now raspotify.service
sudo systemctl mask raspotify.service

# Start pulseaudio
systemctl --user start pulseaudio && sudo usermod -a -G bluetooth pi
 
sudo reboot --reboot