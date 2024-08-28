#!/bin/bash
sudo sh -c "cat <<\EOT > /etc/systemd/system/Resona.service
[Unit]
Description=Resona
ConditionPathExists=/home/pi/bin

[Service]
# You can use 'journalctl Resona.service' to view the logs
StandardOutput=journal
StandardError=journal
ExecStart=/home/pi/bin/Resona --drm
WorkingDirectory=/home/pi/bin
User=pi
Restart=always
RestartSec=2s
StartLimitBurst=3
Environment="PULSE_RUNTIME_PATH=/run/user/1000/pulse/"

[Install]
WantedBy=multi-user.target
EOT"

sudo systemctl daemon-reload

sudo systemctl enable Resona.service

sudo systemctl start Resona.service