<template>
    <v-row>
        <v-col cols="12">
            <v-card variant="text">
                <v-row align="center" no-gutters>
                    <v-col class="large-icon" cols="auto">
                        <v-icon :icon="icon"></v-icon>
                    </v-col>
                    <v-col>
                        <v-card-title>{{ title }}</v-card-title>
                        <v-card-subtitle class="multiline-subtitle">
                            {{ subtitle }}
                        </v-card-subtitle>
                    </v-col>
                    <v-col v-if="wikiKeyWord" cols="auto">
                        <v-btn append-icon="mdi-search-web"
                            :href="`https://dwarffortresswiki.org/index.php/${encodeURIComponent(wikiKeyWord)}`"
                            target="_blank">
                            Search DF Wiki

                            <template v-slot:append>
                                <v-icon color="primary"></v-icon>
                            </template>
                        </v-btn>
                    </v-col>
                </v-row>
            </v-card>
        </v-col>
    </v-row>
    <v-row>
        <slot name="type-specific-before-table"></slot>
    </v-row>
    <v-row>
        <v-col>
            <v-card :title="overviewTitle" :subtitle="`${overviewSubtitle} — search by English or Dwarven names`" variant="text">
                <template v-slot:prepend>
                    <v-icon class="mr-2" icon="mdi-card-search-outline" size="32px"></v-icon>
                </template>
                <template v-slot:text>
                    <v-text-field v-model="searchString" label="Search" placeholder="English or Dwarven name"
                        prepend-inner-icon="mdi-magnify" variant="outlined" hide-details single-line
                        persistent-placeholder>
                        <template v-slot:append-inner>
                            <v-tooltip location="top">
                                <template v-slot:activator="{ props: tProps }">
                                    <v-icon v-bind="tProps" icon="mdi-information-outline" size="small" class="text-medium-emphasis" />
                                </template>
                                Search by English or Dwarven names (e.g. ngathsesh).
                            </v-tooltip>
                        </template>
                    </v-text-field>
                </template>
                <v-card-text>
                    <v-data-table-server v-model:items-per-page="store.objectsPerPage" :headers="tableHeaders"
                        :items="store.objects" :items-length="store.objectsTotalFilteredItems" :search="searchString"
                        :loading="store.isLoading" item-value="id" :items-per-page-options="store.itemsPerPageOptions" @update:options="loadWorldObjects">
                        <template v-slot:item.html="{ item }">
                            <div>
                                <span v-html="asWorldObject(item).html"></span>
                                <span v-if="asWorldObject(item).dwarvenAlias" class="text-caption text-medium-emphasis ml-1">({{ asWorldObject(item).dwarvenAlias }})</span>
                            </div>
                        </template>
                        <template v-slot:item.subtype="{ value }">
                            <span v-html="value"></span>
                        </template>
                    </v-data-table-server>
                </v-card-text>
                <template v-slot:append>
                    <v-chip class="ma-2" color="cyan" label>
                        <v-icon :icon="icon" start></v-icon>
                        {{ title }}: {{ store.objectsTotalItems }}
                    </v-chip>
                </template>
            </v-card>
        </v-col>
    </v-row>
    <v-row>
        <slot name="type-specific-after-table"></slot>
    </v-row>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { components } from '../generated/api-schema';
import { LoadItemsOptionsWithSearch, TableHeader } from '../types/legends';

type WorldObjectDto = components['schemas']['WorldObjectDto'];

function asWorldObject(item: unknown): WorldObjectDto {
    return item as WorldObjectDto;
}

const props = defineProps({
    store: {
        type: Object,
        required: true,
    },
    icon: {
        type: String,
        required: true,
    },
    title: {
        type: String,
        required: true,
    },
    subtitle: {
        type: String,
        required: true,
    },
    overviewTitle: {
        type: String,
        required: true,
    },
    overviewSubtitle: {
        type: String,
        required: true,
    },
    wikiKeyWord: {
        type: String,
        required: false,
    },
    tableHeaders: {
        type: Array as () => TableHeader[],
        required: true,
    },
});

const searchString = ref("")

const loadWorldObjects = async ({ page, itemsPerPage, sortBy, search }: LoadItemsOptionsWithSearch) => {
    await props.store.loadOverview(page, itemsPerPage, sortBy, search)
}

// Load initial data when component mounts
loadWorldObjects({ page: 1, itemsPerPage: props.store.objectEventsPerPage, sortBy: [], search: searchString.value })

watch(searchString, () => {
    loadWorldObjects({ page: 1, itemsPerPage: props.store.objectEventsPerPage, sortBy: [], search: searchString.value })
});
</script>

<style scoped>
.multiline-subtitle {
    white-space: normal;
}
</style>
