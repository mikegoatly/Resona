#!/bin/bash
# Configures librespot (installed by install-dependencies.sh via the raspotify package) to run as a
# Spotify Connect device under the pi user's systemd session, outputting to PulseAudio so Resona's
# audio output / Bluetooth selection applies to Spotify playback too.
#
# Usage: ./configure-spotify.sh [DeviceName]
#   DeviceName defaults to "Resona-<hostname>" and must match the device name configured on the
#   Spotify page of Resona's web UI.

DEVICE_NAME="${1:-Resona-$(hostname)}"
CACHE_DIR="/home/pi/.cache/resona-spotify"

if [ ! -x /usr/bin/librespot ]; then
    echo "/usr/bin/librespot not found - run ./install-dependencies.sh first."
    exit 1
fi

mkdir -p "${CACHE_DIR}"

sudo tee /lib/systemd/user/resona-spotify.service > /dev/null <<EOT
[Unit]
Description=Resona Spotify Connect (librespot)
After=network-online.target sound.target pulseaudio.service
Wants=pulseaudio.service

[Service]
# You can use 'journalctl --user -u resona-spotify.service --lines 100 -f' to view and tail the logs
StandardOutput=journal
StandardError=journal
ExecStart=/usr/bin/librespot --name "${DEVICE_NAME}" --backend pulseaudio --cache ${CACHE_DIR} --disable-audio-cache --initial-volume 50 --bitrate 160
Restart=on-failure
RestartSec=5s

[Install]
WantedBy=default.target
EOT

systemctl --user daemon-reload

systemctl --user enable resona-spotify.service

systemctl --user restart resona-spotify.service

echo "Spotify Connect device \"${DEVICE_NAME}\" configured."
echo "First login: cast to \"${DEVICE_NAME}\" once from the Spotify app on a phone on the same network."
