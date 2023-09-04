#!/bin/bash

SERVICE="Resona.service"

if systemctl list-unit-files --type=service | grep -q "${SERVICE}"; then
    echo "${SERVICE} is installed. Enabling and starting it now."
    sudo systemctl enable "${SERVICE}"
    sudo systemctl start "${SERVICE}"
else
    echo "${SERVICE} is not installed."
fi