import { createRouter, createWebHistory } from 'vue-router'
import { isHostDevice, screenHome, storedScreen } from '@/shared/lib/screen'

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
    // A QR code of the screens (room-spec §7.6): exchanged once for this device's own link.
    {
      path: '/screen/:token',
      name: 'screen',
      component: () => import('@/features/room/screen/ScreenPage.vue'),
      props: true,
      meta: { bare: true },
    },
    // The table shown to the players: to look at.
    {
      path: '/table',
      name: 'table-screen',
      component: () => import('@/features/room/screen/TableScreenPage.vue'),
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
    { path: '/manual', name: 'manual', component: () => import('@/features/manual/pages/ManualPage.vue') },
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

router.beforeEach((to) => screenHome(to.path, storedScreen(), isHostDevice()) ?? true)

declare module 'vue-router' {
  interface RouteMeta {
    /** A page of the players' phones: no top bar. */
    bare?: boolean
    /** The GM's screen of a game: it fills the top bar itself. */
    game?: boolean
  }
}
