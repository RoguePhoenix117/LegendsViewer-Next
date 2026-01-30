<!--suppress ALL -->
<script setup lang="ts">
import {computed, ref, watch} from 'vue';
import { useBookmarkStore } from '../stores/bookmarkStore';
import { useFileSystemStore, UPLOAD_TIMEOUT_MINUTES } from '../stores/fileSystemStore';

const bookmarkStore = useBookmarkStore()
const fileSystemStore = useFileSystemStore()
bookmarkStore.getAll()
bookmarkStore.checkAdminStatus()
fileSystemStore.initialize();

const fileName = ref<string>('')
const selectedFiles = ref<File[] | null>(null)
const tab = ref<string>('select')
const deleteWorldDialog = ref(false)
const worldToDelete = ref<{filePath: string, regionName: string, timestamp: string} | null>(null)
const uploadDialogActive = ref(false)
const openUploadAfterDelete = ref(false)
/** Main -legends.xml filename from the most recent upload; shown as "NEW" at top of list until dialog closes */
const latestUploadedFileName = ref<string | null>(null)

/** Latest export (just uploaded) if it's in the current file list */
const latestExportFile = computed(() => {
  const name = latestUploadedFileName.value;
  const files = fileSystemStore.filesAndSubdirectories.files ?? [];
  return name && files.includes(name) ? name : null;
});

/** Existing exports: all files except the latest one */
const existingExportFiles = computed(() => {
  const files = fileSystemStore.filesAndSubdirectories.files ?? [];
  const latest = latestExportFile.value;
  return latest ? files.filter(f => f !== latest) : files;
});

// Clear "NEW" when dialog closes so it doesn't persist next time
watch(uploadDialogActive, (active) => {
  if (!active) latestUploadedFileName.value = null;
});

// Function to prepare a proper base64 string for png images
const getImageData = (bookmark: any) => {
  if (!bookmark.worldMapImage) {
    return ''; // Return an empty string if there's no image data
  }
  return `data:image/png;base64,${bookmark.worldMapImage}`;
}

const isDialogVisible = computed({
  get() {
    return bookmarkStore.bookmarkWarning != null && bookmarkStore.bookmarkWarning.length > 0;
  },
  set(value: boolean) {
    if (!value) {
      bookmarkStore.bookmarkWarning = ''; // Clear bookmarkWarning on close
    }
  },
});

const closeDialog = () => {
  isDialogVisible.value = false; // Close the dialog and clear the warning
};

const isSnackbarVisible = computed({
  get() {
    return bookmarkStore.bookmarkError != null && bookmarkStore.bookmarkError.length > 0;
  },
  set(value: boolean) {
    if (!value) {
      bookmarkStore.bookmarkError = ''; // Clear bookmarkError on close
    }
  },
});

const closeSnackbar = () => {
  isSnackbarVisible.value = false; // Close the snackbar and clear the error
};

const uploadFiles = async () => {
  if (!selectedFiles.value || selectedFiles.value.length === 0) return;
  
  // Capture main -legends.xml filename for "NEW" badge before clearing
  const mainFile = selectedFiles.value.find(f => f.name.endsWith('-legends.xml'));
  const newLatestName = mainFile ? mainFile.name : null;

  try {
    await fileSystemStore.uploadFiles(selectedFiles.value);
    selectedFiles.value = null;
    // Refresh file list and bookmarks
    await fileSystemStore.loadFiles();
    await bookmarkStore.getAll();
    latestUploadedFileName.value = newLatestName;
    // Switch to Select tab so user can immediately load the world (dialog stays open)
    tab.value = 'select';
  } catch (error) {
    bookmarkStore.bookmarkError = fileSystemStore.uploadError || 'Failed to upload files';
  }
};

const deleteWorldFile = async (fileToDelete: string) => {
  // Determine which related files will be deleted
  let relatedFilesMessage = `"${fileToDelete}"`;
  if (fileToDelete.includes('-legends.xml')) {
    const regionId = fileToDelete.replace('-legends.xml', '');
    const relatedFiles = [
      `${regionId}-legends.xml`,
      `${regionId}-legends_plus.xml`,
      `${regionId}-world_history.txt`,
      `${regionId}-world_map.bmp`,
      `${regionId}-world_sites_and_pops.txt`
    ];
    relatedFilesMessage = relatedFiles.join(', ');
  } else if (fileToDelete.includes('-legends_plus.xml')) {
    const regionId = fileToDelete.replace('-legends_plus.xml', '');
    const relatedFiles = [
      `${regionId}-legends.xml`,
      `${regionId}-legends_plus.xml`,
      `${regionId}-world_history.txt`,
      `${regionId}-world_map.bmp`,
      `${regionId}-world_sites_and_pops.txt`
    ];
    relatedFilesMessage = relatedFiles.join(', ');
  }
  
  if (!confirm(`Are you sure you want to delete this world?\n\nThis will permanently remove the following files from the server:\n${relatedFilesMessage}\n\nThis action cannot be undone.`)) {
    return;
  }
  
  try {
    await fileSystemStore.deleteFile(fileToDelete);
    // Clear selected file if it was the deleted one
    if (fileToDelete === fileName.value) {
      fileName.value = '';
    }
    // Refresh file list
    await fileSystemStore.loadFiles();
  } catch (error) {
    bookmarkStore.bookmarkError = error instanceof Error ? error.message : 'Failed to delete file';
  }
};

const openDeleteWorldDialog = (bookmark: any, replaceAfterDelete: boolean = false) => {
  // Extract filename from bookmark filePath by replacing {TIMESTAMP} with actual timestamp
  const filePath = bookmark.filePath ?? '';
  const timestamp = bookmark.latestTimestamp ?? '';
  // Replace {TIMESTAMP} placeholder and extract just the filename (not the full path)
  const fullPath = filePath.replace('{TIMESTAMP}', timestamp);
  const fileName = fullPath.split('/').pop() || fullPath; // Get just the filename
  const regionName = bookmark.worldRegionName ?? bookmark.worldName ?? 'Unknown';
  
  worldToDelete.value = {
    filePath: fileName,
    regionName: regionName,
    timestamp: timestamp
  };
  openUploadAfterDelete.value = replaceAfterDelete;
  deleteWorldDialog.value = true;
};

const getRelatedFiles = (filePath: string): string[] => {
  if (filePath.includes('-legends.xml')) {
    const regionId = filePath.replace('-legends.xml', '');
    return [
      `${regionId}-legends.xml`,
      `${regionId}-legends_plus.xml`,
      `${regionId}-world_history.txt`,
      `${regionId}-world_map.bmp`,
      `${regionId}-world_sites_and_pops.txt`
    ];
  } else if (filePath.includes('-legends_plus.xml')) {
    const regionId = filePath.replace('-legends_plus.xml', '');
    return [
      `${regionId}-legends.xml`,
      `${regionId}-legends_plus.xml`,
      `${regionId}-world_history.txt`,
      `${regionId}-world_map.bmp`,
      `${regionId}-world_sites_and_pops.txt`
    ];
  }
  return [filePath];
};

const confirmDeleteWorld = async () => {
  if (!worldToDelete.value) return;
  
  const fileToDelete = worldToDelete.value.filePath;
  const shouldOpenUpload = openUploadAfterDelete.value;
  deleteWorldDialog.value = false;
  
  try {
    await fileSystemStore.deleteFile(fileToDelete);
    // Refresh bookmarks and file list
    await bookmarkStore.getAll();
    await fileSystemStore.loadFiles();
    
    // If this was a "delete and replace", open the upload dialog
    if (shouldOpenUpload) {
      tab.value = 'upload';
      uploadDialogActive.value = true;
    }
    
    worldToDelete.value = null;
    openUploadAfterDelete.value = false;
  } catch (error) {
    bookmarkStore.bookmarkError = error instanceof Error ? error.message : 'Failed to delete world';
    worldToDelete.value = null;
    openUploadAfterDelete.value = false;
  }
};

</script>

<template>
  <v-row dense>
    <v-col v-if="bookmarkStore.isAdmin" cols="12" md="3">
      <v-card class="mx-auto" max-width="320">
        <v-container>
          <v-icon icon="mdi-earth-box-plus" size="300"></v-icon>
        </v-container>

        <v-card-title>
          Explore a new world
        </v-card-title>

        <v-card-subtitle>
          Select an exported legends XML file
        </v-card-subtitle>

        <v-card-actions>
          <v-dialog v-model="uploadDialogActive" width="auto" min-width="480">
            <template v-slot:activator="{ props: activatorProps }">
              <v-btn color="orange-lighten-2" prepend-icon="mdi-earth" text="Select" variant="tonal" class="ml-1"
                v-bind="activatorProps" :disabled="bookmarkStore.isLoading" :loading="bookmarkStore.isLoading"></v-btn>
            </template>

            <template v-slot:default>
              <v-card prepend-icon="mdi-earth" title="Manage World Exports">
                <v-tabs v-model="tab" class="mb-2">
                  <v-tab value="select">Select</v-tab>
                  <v-tab value="upload">Upload</v-tab>
                </v-tabs>

                <v-window v-model="tab">
                  <!-- Select Tab -->
                  <v-window-item value="select">
                    <v-card-text class="px-4" style="max-width: 720px;">
                      <v-alert type="info" variant="tonal" style="margin-bottom: 16px;">
                        This file list shows only the main export file (e.g., &lt;savename&gt;-&lt;timestamp&gt;-legends.xml). 
                        If other files related to the same export are present,
                        they will be automatically detected and included when you select the main file.
                      </v-alert> 

                      <v-form>
                        <v-text-field v-model="fileName" readonly label="Selected File"></v-text-field>
                      </v-form>

                      <v-list density="compact" min-height="400" max-height="400" scrollable>
                        <v-list-subheader>Available World Exports</v-list-subheader>
                        <template v-if="latestExportFile">
                          <v-list-subheader class="text-uppercase text-caption font-weight-bold">Latest</v-list-subheader>
                          <v-list-item
                            :value="latestExportFile"
                            color="primary"
                            variant="plain"
                            :class="fileName === latestExportFile ? 'bg-primary-lighten-5' : ''"
                            @click="fileName = latestExportFile">
                            <template v-slot:prepend>
                              <v-icon icon="mdi-file-xml-box"></v-icon>
                            </template>
                            <v-list-item-title class="d-flex align-center">
                              <span class="text-uppercase font-weight-bold mr-2 text-orange">NEW</span>
                              <span>{{ latestExportFile }}</span>
                            </v-list-item-title>
                            <template v-if="bookmarkStore.isAdmin" v-slot:append>
                              <v-btn
                                icon="mdi-delete-outline"
                                variant="text"
                                color="error"
                                density="compact"
                                size="small"
                                :disabled="fileSystemStore.loading"
                                @click.stop="deleteWorldFile(latestExportFile)">
                              </v-btn>
                            </template>
                          </v-list-item>
                        </template>
                        <template v-if="existingExportFiles.length > 0">
                          <v-list-subheader class="text-uppercase text-caption font-weight-bold">Existing</v-list-subheader>
                          <v-list-item v-for="(item, i) in existingExportFiles" :key="i"
                            :value="item" color="primary" variant="plain"
                            :class="fileName === item ? 'bg-primary-lighten-5' : ''"
                            @click="fileName = item">
                            <template v-slot:prepend>
                              <v-icon icon="mdi-file-xml-box"></v-icon>
                            </template>
                            <v-list-item-title v-text="item"></v-list-item-title>
                            <template v-if="bookmarkStore.isAdmin" v-slot:append>
                              <v-btn
                                icon="mdi-delete-outline"
                                variant="text"
                                color="error"
                                density="compact"
                                size="small"
                                :disabled="fileSystemStore.loading"
                                @click.stop="deleteWorldFile(item)">
                              </v-btn>
                            </template>
                          </v-list-item>
                        </template>
                        <v-list-item v-if="!fileSystemStore.filesAndSubdirectories.files || fileSystemStore.filesAndSubdirectories.files.length === 0" disabled>
                          <v-list-item-title>No world export files found in data directory</v-list-item-title>
                        </v-list-item>
                      </v-list>
                    </v-card-text>

                    <v-divider></v-divider>

                    <v-card-actions>
                      <v-btn text="Close" @click="uploadDialogActive = false"></v-btn>

                      <v-spacer></v-spacer>

                      <v-btn color="orange-lighten-2" text="Load World" variant="tonal"
                        :disabled="fileName == null || fileName == ''"
                        :loading="bookmarkStore.isLoading"
                        @click="bookmarkStore.loadByFileName(fileName); uploadDialogActive = false;"></v-btn>
                    </v-card-actions>
                  </v-window-item>

                  <!-- Upload Tab -->
                  <v-window-item value="upload">
                    <v-card-text class="px-4" style="max-width: 720px;">
                      <v-alert type="info" variant="tonal" style="margin-bottom: 16px;">
                        Upload world export files (XML, TXT, BMP). Maximum file size: 1 GB.
                        Upload all related files for a world export (legends.xml, legends_plus.xml, etc.).
                      </v-alert>

                      <v-file-input
                        v-model="selectedFiles"
                        label="Select files to upload"
                        accept=".xml,.txt,.bmp"
                        prepend-icon="mdi-file-upload"
                        show-size
                        :disabled="fileSystemStore.uploading"
                        :multiple="true"
                        hint="You can select multiple files at once (e.g., legends.xml and legends_plus.xml)"
                        persistent-hint
                      ></v-file-input>

                      <v-progress-linear
                        v-if="fileSystemStore.uploading"
                        :model-value="fileSystemStore.uploadProgress"
                        color="primary"
                        height="8"
                        striped
                        rounded
                        class="mt-3"
                      ></v-progress-linear>
                      <p v-if="fileSystemStore.uploading" class="text-caption text-medium-emphasis mt-1">
                        Uploading… {{ fileSystemStore.uploadProgress }}%. Large files can take several minutes (timeout: {{ UPLOAD_TIMEOUT_MINUTES }} min).
                      </p>

                      <v-alert v-if="fileSystemStore.uploadError" type="error" variant="tonal" class="mt-2">
                        {{ fileSystemStore.uploadError }}
                      </v-alert>
                    </v-card-text>

                    <v-divider></v-divider>

                    <v-card-actions>
                      <v-btn text="Close" @click="uploadDialogActive = false"></v-btn>

                      <v-spacer></v-spacer>

                      <v-btn color="primary" text="Upload" variant="flat"
                        :disabled="!selectedFiles || selectedFiles.length === 0"
                        :loading="fileSystemStore.uploading"
                        @click="uploadFiles"></v-btn>
                    </v-card-actions>
                  </v-window-item>
                </v-window>
              </v-card>
            </template>
          </v-dialog>
          <v-spacer></v-spacer>

        </v-card-actions>
      </v-card>
    </v-col>
    <template v-for="(bookmark, i) in bookmarkStore.bookmarks.slice().reverse()" :key="i">
      <v-col v-if="bookmark != null && bookmark.filePath" :for="i" cols="12" md="3">
        <v-card class="mx-auto" max-width="320">
          <v-container>
            <v-img height="300px" width="300px" class="pixelated-image" :src="getImageData(bookmark)"></v-img>
          </v-container>

          <v-card-title>
            {{ (bookmark.worldName != null && bookmark.worldName.length > 0 ?
              bookmark.worldName :
              bookmark.worldRegionName)
            }}
            <v-chip class="float-right">
              {{ bookmark.worldWidth + " x " + bookmark.worldHeight }}
            </v-chip>
          </v-card-title>

          <v-card-subtitle>
            {{ (bookmark.worldAlternativeName != null && bookmark.worldAlternativeName.length > 0 ?
              bookmark.worldAlternativeName :
              '-')
            }}
          </v-card-subtitle>

          <v-card-actions>
            <!-- Admin: Load button for unloaded worlds -->
            <v-btn
              v-if="bookmarkStore.isAdmin && bookmark.filePath && (bookmark.state !== 'Loaded' || bookmark.latestTimestamp !== bookmark.loadedTimestamp)"
              :loading="bookmark.state === 'Loading'" color="blue" text="Load" :disabled="bookmarkStore.isLoading"
              variant="tonal" class="ml-1"
              @click="bookmarkStore.loadByFullPath(bookmark.filePath ?? '', bookmark.latestTimestamp ?? '')">
            </v-btn>
            <!-- Non-admin: "Not Loaded" button (same orange as Select; click shows admin message) -->
            <v-btn
              v-else-if="!bookmarkStore.isAdmin && bookmark.filePath && (bookmark.state !== 'Loaded' || bookmark.latestTimestamp !== bookmark.loadedTimestamp)"
              color="orange-lighten-2"
              text="Not Loaded"
              variant="tonal"
              class="ml-1"
              @click="bookmarkStore.bookmarkError = 'You require admin permissions to load a new world into memory.'">
            </v-btn>
            <!-- Loaded world: Explore for both admin and non-admin -->
            <v-btn
              v-if="bookmark.filePath && bookmark.state === 'Loaded' && bookmark.latestTimestamp === bookmark.loadedTimestamp"
              color="green-lighten-2" text="Explore" variant="tonal" class="ml-1" :disabled="bookmarkStore.isLoading"
              to="/world">
            </v-btn>
            <v-menu
              v-if="bookmarkStore.isAdmin && bookmark.filePath"
              :disabled="bookmarkStore.isLoading"
              transition="slide-x-transition">
              <template v-slot:activator="{ props }">
                <v-btn v-bind="props" icon="mdi-dots-vertical" variant="text" density="compact" size="small"></v-btn>
              </template>

              <v-list>
                <v-list-item
                  :disabled="bookmarkStore.isLoading"
                  @click="openDeleteWorldDialog(bookmark, false)">
                  <v-list-item-title>
                    <v-icon class="mt-n1" color="error" icon="mdi-delete-outline"></v-icon>
                    Delete World
                  </v-list-item-title>
                </v-list-item>
                <v-list-item
                  :disabled="bookmarkStore.isLoading"
                  @click="openDeleteWorldDialog(bookmark, true)">
                  <v-list-item-title>
                    <v-icon class="mt-n1" color="primary" icon="mdi-delete-sweep"></v-icon>
                    Delete and Replace
                  </v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
            <v-spacer></v-spacer>
            <v-menu transition="slide-x-transition">
              <template v-slot:activator="{ props }">
                <v-btn v-bind="props" variant="text"
                  :disabled="bookmark.worldTimestamps == null || bookmark.worldTimestamps.length <= 1">
                  {{ bookmark.latestTimestamp }}
                  <template v-if="bookmark.worldTimestamps != null && bookmark.worldTimestamps.length > 1"
                    v-slot:append>
                    <v-icon icon="mdi-menu-down"></v-icon>
                  </template>
                </v-btn>
              </template>

              <v-list>
                <v-list-item v-for="(item, i) in bookmark.worldTimestamps ?? []" :key="i"
                  @click="bookmark.latestTimestamp = item">
                  <v-list-item-title>{{ item }}</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
            <!-- <v-combobox v-model="bookmark.latestTimestamp" :items="bookmark.worldTimestamps ?? []" density="compact"
            label="Timestamps" width="160" :disabled="bookmarkStore.isLoading"></v-combobox> -->
          </v-card-actions>
        </v-card>
      </v-col>
    </template>
  </v-row>
  <v-dialog v-model="isDialogVisible" transition="dialog-top-transition" width="500px">
    <v-card max-width="400" prepend-icon="mdi-alert-outline" title="Warning" :text="bookmarkStore.bookmarkWarning">
      <template v-slot:actions>
        <v-btn class="ms-auto" text="Ok" @click="closeDialog"></v-btn>
      </template>
    </v-card>
  </v-dialog>
  <v-snackbar v-model="isSnackbarVisible" multi-line top color="error">
    {{ bookmarkStore.bookmarkError }}

    <template v-slot:actions>
      <v-btn color="black" variant="tonal" @click="closeSnackbar">
        Close
      </v-btn>
    </template>
  </v-snackbar>
  <v-dialog v-model="deleteWorldDialog" max-width="500px" persistent>
    <v-card>
      <v-card-title class="text-h5">
        <v-icon icon="mdi-alert" color="error" class="mr-2"></v-icon>
        Delete World
      </v-card-title>
      <v-card-text>
        <p class="text-body-1 mb-2">
          Are you sure you want to delete this world?
        </p>
        <p class="text-body-2 mb-2" v-if="worldToDelete">
          <strong>{{ worldToDelete.regionName }}</strong> ({{ worldToDelete.timestamp }})
        </p>
        <v-alert type="warning" variant="tonal" class="mt-2">
          This will permanently remove the following files from the server:
          <ul class="mt-2" v-if="worldToDelete">
            <li v-for="file in getRelatedFiles(worldToDelete.filePath)" :key="file">{{ file }}</li>
          </ul>
          <p class="mt-2 mb-0"><strong>This action cannot be undone.</strong></p>
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer></v-spacer>
        <v-btn text="Cancel" @click="deleteWorldDialog = false; worldToDelete = null" :disabled="fileSystemStore.loading"></v-btn>
        <v-btn color="error" text="Delete" variant="flat" @click="confirmDeleteWorld" :loading="fileSystemStore.loading" :disabled="fileSystemStore.loading"></v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
