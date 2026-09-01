import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'lobby',
      component: () => import('@/views/LobbyView.vue'),
    },
    {
      path: '/r/:code',
      name: 'room',
      component: () => import('@/views/RoomView.vue'),
      props: true,
    },
    {
      // 邀请链接落地页。刻意做得极短，方便口头转述和粘贴。
      path: '/j/:code',
      name: 'invite',
      component: () => import('@/views/InviteView.vue'),
      props: true,
    },
    {
      path: '/h/:gameId',
      name: 'replay',
      component: () => import('@/views/ReplayView.vue'),
      props: true,
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

export default router
