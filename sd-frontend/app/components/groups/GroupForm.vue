<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader :title="title" />
      </template>
      <UForm
        :state="form"
        class="space-y-4"
        @submit="onSubmit"
      >
        <div class="grid grid-cols-1 gap-4">
          <UFormField
            :label="$t('groups.groupName')"
            name="name"
            required
          >
            <UInput
              v-model="form.name"
              :placeholder="$t('groups.enterGroupName')"
              required
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('groups.description')"
            name="description"
          >
            <UInput
              v-model="form.description"
              :placeholder="$t('groups.enterDescription')"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('groups.useAliases')"
            name="useAliases"
            :description="$t('groups.useAliasesDescription')"
          >
            <USwitch
              v-model="form.useAliases"
              :disabled="disabledAliases"
            />
          </UFormField>
          <p
            v-if="disabledAliases"
            class="text-xs text-muted -mt-2"
          >
            {{ $t('groups.aliasCannotChange') }}
          </p>
        </div>
        <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
          <UButton
            :label="$t('common.back')"
            variant="ghost"
            color="neutral"
            @click="emit('cancel')"
          />
          <UButton
            type="submit"
            :label="submitLabel"
            :loading="loading"
          />
        </div>
      </UForm>
    </UCard>
  </div>
</template>

<script setup lang="ts">
interface GroupFormData {
  name: string
  description: string
  useAliases: boolean
}

interface Props {
  title: string
  submitLabel: string
  initialData?: GroupFormData
  loading?: boolean
  disabledAliases?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  initialData: () => ({ name: '', description: '', useAliases: false }),
  loading: false,
  disabledAliases: false,
})

const emit = defineEmits<{
  submit: [data: GroupFormData]
  cancel: []
}>()

const form = ref<GroupFormData>({
  name: '',
  description: '',
  useAliases: false,
})

watch(() => props.initialData, (newData) => {
  form.value = {
    name: newData?.name ?? '',
    description: newData?.description ?? '',
    useAliases: newData?.useAliases ?? false,
  }
}, { deep: true })

function onSubmit() {
  emit('submit', { ...form.value })
}
</script>
