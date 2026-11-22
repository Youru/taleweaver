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
    // The GM's second screen (a tablet): keeps the token of the QR code, then shows the table.
    {
      path: '/screen/:gameId/:token',
      name: 'screen',
      component: () => import('@/features/room/screen/ScreenPage.vue'),
      props: true,
      meta: { bare: true },
    },
    { path: '/', name: 'home', component: () => import('@/features/home/pages/HomePage.vue') },
    {
      path: '/games/:id',
      name: 'play',
      component: () => import('@/features/games/pages/PlayPage.vue'),
      props: true,
      meta: { game: true },
    },
    { path: '/rules', name: 'rules', component: () => import('@/features/reference/pages/RulesPage.vue') },
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
    /** The GM's screen of a game: it fills the top bar itself. */
    game?: boolean
  }
}
