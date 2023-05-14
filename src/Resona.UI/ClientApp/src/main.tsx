import React from 'react'
import ReactDOM from 'react-dom'
import App from './App'
import './core.scss'

import {
    QueryClient,
    QueryClientProvider,
} from '@tanstack/react-query'
import { ThemeProvider, createTheme } from '@mui/material/styles'

const queryClient = new QueryClient()

const theme = createTheme({
    palette: {
        primary: {
            main: '#9F6BA0',
        },
        secondary: {
            main: '#638763',
        },
        success: {
            main: '#008000', // Green hex value
        },
        warning: {
            main: '#B8860B', // DarkGoldenrod hex value
        },
        error: {
            main: '#FF0000', // Red hex value
        },
        text: { primary: '#000000', secondary: '#000000' },
    },
});

ReactDOM.render(
    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <QueryClientProvider client={queryClient}>
                <App />
            </QueryClientProvider>
        </ThemeProvider>
    </React.StrictMode>,
    document.getElementById('root')
)
