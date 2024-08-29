#!/bin/bash
sudo sh -c "cat <<\EOT > /lib/systemd/user/Resona.service
[Unit]
Description=Resona
ConditionPathExists=/home/pi/bin
After=sound.target pulseaudio.service 
Wants=pulseaudio.service

[Service]
# You can use 'journalctl --user -u Resona.service --lines 100 -f' to view and tail the logs
StandardOutput=journal
StandardError=journal
ExecStart=/home/pi/bin/Resona --drm
WorkingDirectory=/home/pi/bin
Restart=always
RestartSec=2s
StartLimitBurst=3

[Install]
WantedBy=default.target
EOT"

systemctl --user daemon-reload

systemctl --user enable Resona.service

systemctl --user start Resona.service