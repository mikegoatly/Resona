#!/bin/bash

SERVICE="Resona.service"

if systemctl list-unit-files --user --type=service | grep -q "${SERVICE}"; then
    echo "${SERVICE} is installed. Enabling and starting it now."
    sudo --user systemctl enable "${SERVICE}"
    sudo --user systemctl start "${SERVICE}"
else
    echo "${SERVICE} is not installed."
fi