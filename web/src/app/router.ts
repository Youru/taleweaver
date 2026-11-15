import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
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
