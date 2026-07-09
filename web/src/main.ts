// =====================================================================
// Boot do Marble GP — substitui GameManager.Awake + AppController.Start.
// =====================================================================

import './styles/base.css';
import './styles/screens.css';
import './styles/race.css';

import { Game } from './game/state';
import { bindNotificationLifecycle } from './game/notifications';
import { initRouter } from './ui/router';
import { goMenu, goProfile } from './ui/flow';

const app = document.getElementById('app');
if (!app) throw new Error('#app não encontrado');

initRouter(app);
bindNotificationLifecycle();

// Perfil criado? Menu. Senão, primeira execução.
if (Game.profile.created) goMenu();
else goProfile();
