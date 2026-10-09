import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { setupIonicReact } from '@ionic/react';
import '@ionic/react/css/core.css';
import '@ionic/react/css/normalize.css';
import '@ionic/react/css/structure.css';
import '@ionic/react/css/typography.css';
import NativeShell from './NativeShell';
import './styles.css';

setupIonicReact();
createRoot(document.getElementById('root')!).render(<StrictMode><NativeShell /></StrictMode>);
