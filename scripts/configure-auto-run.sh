#!/bin/bash
sudo sh -c "cat <<\EOT > /lib/systemd/user/Resona.service
[Unit]
Description=Resona
ConditionPathExists=/home/pi/bin

[Service]
# You can use 'journalctl --user -u Resona.service' to view the logs
StandardOutput=journal
StandardError=journal
ExecStart=/home/pi/bin/Resona --drm
WorkingDirectory=/home/pi/bin
Restart=always
RestartSec=0s

[Install]
WantedBy=default.target
EOT"

systemctl --user daemon-reload

systemctl --user enable Resona.service

systemctl --user start Resona.service