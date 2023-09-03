#!/bin/bash
sudo sh -c "cat <<\EOT > /etc/systemd/system/Resona.service
[Unit]
Description=Resona
ConditionPathExists=/home/pi/bin

[Service]
# You can use 'journalctl --user -u Resona.service' to view the logs
StandardOutput=journal
StandardError=journal
ExecStart=/home/pi/bin/Resona --drm
WorkingDirectory=/home/pi/bin
User=pi
Restart=always
RestartSec=0s

[Install]
WantedBy=multi-user.target
EOT"

sudo systemctl daemon-reload

sudo systemctl enable Resona.service

sudo systemctl start Resona.service