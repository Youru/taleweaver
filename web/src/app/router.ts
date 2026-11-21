import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    // The players' phones (room-spec §10.4): no top bar, the whole screen for the adventurer.
    {
      path: '/join/:code?',
      name: 'join',
      component: () => import('@/features/room/join/JoinPage.vue'),
      props: true,
      meta: { bare: true },
    },
    {
      path: '/play/gm/:gameId/:adventurer',
      name: 'player-gm',
      component: () => import('@/features/room/player/GmPlayerPage.vue'),
      props: true,
      meta: { bare: true },
    },
    {
      path: '/play/:code',
      name: 'player',
      component: () => import('@/features/room/player/PlayerPage.vue'),
      props: true,
      meta: { bare: true },
    },
    { path: '/', name: 'home', component: () => import('@/features/home/pages/HomePage.vue') },
    { path: '/games/:id', name: 'play', component: () => import('@/features/games/pages/PlayPage.vue'), props: true },
    {
      path: '/workshop',
      name: 'workshop',
      component: () => import('@/features/workshop/pages/WorkshopPage.vue'),
    },
    {
      path: '/workshop/print',
      name: 'workshop-print',
      component: () => import('@/features/workshop/pages/PrintPage.vue'),
    },
    {
      path: '/workshop/:name',
      name: 'workshop-editor',
      component: () => import('@/features/workshop/pages/EditorPage.vue'),
      props: true,
    },
    {
      path: '/games/:id/table',
      name: 'table',
      component: () => import('@/features/table/pages/TablePage.vue'),
      props: true,
    },
  ],
})

declare module 'vue-router' {
  interface RouteMeta {
    /** A page of the players' phones: no top bar. */
    bare?: boolean
  }
}
