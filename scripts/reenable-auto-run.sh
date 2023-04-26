#!/bin/bash

SERVICE="Resona.service"

if systemctl --user list-unit-files --type=service | grep -q "${SERVICE}"; then
    echo "${SERVICE} is installed. Enabling and starting it now."
    systemctl --user enable "${SERVICE}"
    systemctl --user start "${SERVICE}"
else
    echo "${SERVICE} is not installed."
fi