#!/bin/bash

SERVICE="Resona.service"

if systemctl list-unit-files --user --type=service | grep -q "${SERVICE}"; then
    echo "${SERVICE} is installed. Stopping and disabling it now."
    systemctl --user stop "${SERVICE}"
    systemctl --user disable "${SERVICE}"
else
    echo "${SERVICE} is not installed."
fi