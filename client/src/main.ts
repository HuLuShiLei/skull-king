import { createPinia } from 'pinia'
import { createApp } from 'vue'

import App from './App.vue'
import router from './router'
import { installTheme } from './theme'
import { installViewportHeight } from './viewport'
import './styles/app.css'

installTheme()
installViewportHeight()

createApp(App).use(createPinia()).use(router).mount('#app')
