import React, { useState } from 'react';
import { NavLink as RouterLink } from 'react-router-dom';
import logo from '../Resona.png';
import './Navigation.scss';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import UploadQueue from './UploadQueue/UploadQueue';
import { Box } from '@mui/material';

const Navigation = () => {
    return (
        <AppBar position="sticky" className="app-bar">
            <Container maxWidth="xl">
                <Toolbar className="app-bar-buttons">
                    <div className="left">
                        <Button component={RouterLink} to="/" >
                            <img src={logo} alt="Resona logo" className="logo" width={50} height={50} />
                            <Typography variant="h6">
                                Resona
                            </Typography>
                        </Button>
                        <Button component={RouterLink} to="/library/audiobook" >
                            <Typography variant="button">
                                Audiobooks
                            </Typography>
                        </Button>
                        <Button component={RouterLink} to="/library/music" >
                            <Typography variant="button">
                                Music
                            </Typography>
                        </Button>
                        <Button component={RouterLink} to="/library/sleep" >
                            <Typography variant="button">
                                Sleep
                            </Typography>
                        </Button>
                    </div>
                    <Box className="upload-queue">
                        <UploadQueue />
                    </Box>
                </Toolbar>
            </Container>
        </AppBar>
    );
};
export default Navigation;