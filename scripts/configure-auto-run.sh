#!/bin/bash
sudo sh -c "cat <<\EOT > /etc/systemd/system/
[Unit]
Description=Resona
After=multi-user.target

[Service]
#Redirecting output and error to log files is useful when debugging start up issues
#StandardOutput=file:/home/pi/logs/logfile_stdout.log
#StandardError=file:/home/pi/logs/logfile_stderr.log
User=pi
ExecStart=/home/pi/bin/Resona --drm
WorkingDirectory=/home/pi/bin
Restart=always
RestartSec=0s

[Install]
WantedBy=multi-user.target
EOT"

sudo systemctl daemon-reload

sudo systemctl enable Resona.service

sudo systemctl start Resona.service