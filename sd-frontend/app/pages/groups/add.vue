<template>
  <div class="flex flex-col items-center justify-center p-4">
    <UCard class="w-full max-w-md">
      <template #header>
        <h2 class="text-xl font-bold text-center">
          Create Group
        </h2>
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
        <UFormField
          label="Group Name"
          name="name"
          required
        >
          <UInput
            v-model="form.name"
            placeholder="Enter group name"
            required
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UFormField
          label="Description"
          name="description"
        >
          <UInput
            v-model="form.description"
            placeholder="Enter description (optional)"
            size="lg"
            class="w-full"
          />
        </UFormField>
        <UButton
          type="submit"
          label="Create"
          block
          size="lg"
          :loading="isLoading"
        />
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
const { createGroup, isLoading } = useGroups()

const form = ref({ name: '', description: '' })

async function onSubmit() {
  try {
    const group = await createGroup({ name: form.value.name, description: form.value.description })
    if (group) {
      form.value.name = ''
      form.value.description = ''
      navigateTo(`/groups/${group.id}`)
    }
  }
  catch (err) {
    console.error('Error creating group:', err)
  }
}

useHead({
  title: 'Create Group',
})

definePageMeta({
  middleware: 'auth',
})
</script>
