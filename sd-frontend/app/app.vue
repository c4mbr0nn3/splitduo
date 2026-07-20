<template>
  <UApp>
    <NuxtLoadingIndicator
      color="repeating-linear-gradient(to right, #14b8a6 0%, #5eead4 50%, #0d9488 100%)"
      :height="3"
      :throttle="100"
      :duration="1500"
      error-color="repeating-linear-gradient(to right, #f43f5e 0%, #fb7185 50%, #e11d48 100%)"
    />
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
    <PwaUpdate />
    <PwaInstall />
    <PwaOffline />
  </UApp>
</template>
