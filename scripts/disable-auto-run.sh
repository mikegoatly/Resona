#!/bin/bash

SERVICE="Resona.service"

if systemctl list-unit-files --type=service | grep -q "${SERVICE}"; then
    echo "${SERVICE} is installed. Stopping and disabling it now."
    sudo systemctl stop "${SERVICE}"
    sudo systemctl disable "${SERVICE}"
else
    echo "${SERVICE} is not installed."
fi