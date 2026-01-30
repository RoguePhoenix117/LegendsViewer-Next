import { defineStore } from 'pinia';
import client from "../apiClient"; // Import the global client
import { components } from '../generated/api-schema'; // Import from the OpenAPI schema

export type Bookmark = components['schemas']['Bookmark'];

export const useBookmarkStore = defineStore('bookmark', {
  state: () => ({
    bookmarks: [] as Bookmark[],
    bookmarkError: '' as string,
    bookmarkWarning: '' as string,
    isLoadingNewWorld: false as boolean,
    isAdmin: false as boolean
  }),
  getters: {
    isLoadingExistingWorld: (state) => {
      return state.bookmarks.some(bookmark => bookmark.state === 'Loading');
    },
    isLoading: (state) => {
      return state.isLoadingNewWorld || state.bookmarks.some(bookmark => bookmark.state === 'Loading');
    },
    isLoaded: (state) => {
      return state.bookmarks.some(bookmark => bookmark.state === 'Loaded');
    },
  },
  actions: {
    async loadByFullPath(filePath: string, latestTimestamp: string) {
      // Extract filename from path for new endpoint
      // Handle both full paths and paths with {TIMESTAMP} placeholder
      const resolvedPath = filePath.replace("{TIMESTAMP}", latestTimestamp);
      const fileName = resolvedPath.split(/[/\\]/).pop() || resolvedPath;
      
      // Set the state of the bookmark to 'Loading' if it exists
      let existingBookmark = this.bookmarks.find(bookmark => bookmark.filePath === filePath);
      if (existingBookmark) {
        existingBookmark.state = 'Loading';
      }
      else {
        this.isLoadingNewWorld = true;
      }

      // Use new endpoint with filename only
      // @ts-ignore - API schema will be regenerated after backend changes
      const { data, error } = await client.POST("/api/Bookmark/loadByFileName" as any, {
        body: fileName
      });

      if (error !== undefined) {
        console.error(error);
        let existingBookmark = this.bookmarks.find(bookmark => bookmark.filePath === filePath);
        if (existingBookmark) {
          existingBookmark.state = 'Default';
        }
        this.isLoadingNewWorld = false;
        this.bookmarkError = error.title ?? error.type ?? '';
      } else if (data) {
        const newBookmark = data as Bookmark;
        if (newBookmark.worldName == null || newBookmark.worldName.length == 0) {
          this.bookmarkWarning = 'The legends_plus.xml file was not found. Dwarf Fortress currently exports only a limited amount of legends data. To access more detailed information, including proper maps and other important features, please install DFHack, which will automatically export the additional data.'
        }
        // Check if the bookmark already exists
        const index = this.bookmarks.findIndex(bookmark => bookmark.filePath === newBookmark.filePath);
        this.bookmarks.forEach(b => b.state = 'Default')
        if (index !== -1) {
          // Update the existing bookmark
          this.bookmarks[index] = newBookmark;
        } else {
          // Add the new bookmark to the array
          this.bookmarks.push(newBookmark);
        }

        this.isLoadingNewWorld = false;
      }
    },
    async deleteByFullPath(filePath: string, latestTimestamp: string) {
      // Set the state of the bookmark to 'Loading' if it exists
      let existingBookmark = this.bookmarks.find(bookmark => bookmark.filePath === filePath);
      if (existingBookmark) {
        existingBookmark.state = 'Loading';
      }
      else {
        this.isLoadingNewWorld = true;
      }

      const { data, error } = await client.DELETE("/api/Bookmark/{filePath}", {
        params: {
          path: {
            filePath: filePath.replace("{TIMESTAMP}", latestTimestamp)
          }
        }
      });

      if (error !== undefined) {
        console.error(error);
        let existingBookmark = this.bookmarks.find(bookmark => bookmark.filePath === filePath);
        if (existingBookmark) {
          existingBookmark.state = 'Default';
        }
        this.isLoadingNewWorld = false;
        this.bookmarkError = error.title ?? error.type ?? ''
      } else if (data) {
        const newBookmark = data as Bookmark | null | undefined;

        const index = this.bookmarks.findIndex(bookmark => bookmark.filePath === filePath);
        if (newBookmark != null && index !== -1) {
          // Update the existing bookmark
          this.bookmarks[index] = newBookmark;
        } else {
          // Remove the bookmark if newBookmark is null or undefined
          this.bookmarks.splice(index, 1);
          this.bookmarks = this.bookmarks
        }

        this.isLoadingNewWorld = false;
      }
    },
    async loadByFileName(fileName: string) {
      // Set loading state
      this.isLoadingNewWorld = true;

      // @ts-ignore - API schema will be regenerated after backend changes
      const { data, error } = await client.POST("/api/Bookmark/loadByFileName" as any, {
        body: fileName
      });

      if (error !== undefined) {
        console.error(error);
        this.isLoadingNewWorld = false;
        this.bookmarkError = error.title ?? error.type ?? '';
      } else if (data) {
        const newBookmark = data as Bookmark;
        if (newBookmark.worldName == null || newBookmark.worldName.length == 0) {
          this.bookmarkWarning = 'The legends_plus.xml file was not found. Dwarf Fortress currently exports only a limited amount of legends data. To access more detailed information, including proper maps and other important features, please install DFHack, which will automatically export the additional data.'
        }

        // Check if the bookmark already exists
        const index = this.bookmarks.findIndex(bookmark => bookmark.filePath === newBookmark.filePath);
        this.bookmarks.forEach(b => b.state = 'Default')

        if (index !== -1) {
          // Update the existing bookmark
          this.bookmarks[index] = newBookmark;
        } else {
          // Add the new bookmark to the array
          this.bookmarks.push(newBookmark);
        }

        this.isLoadingNewWorld = false;
      }
    },
    async loadByFolderAndFile(_folderPath: string, fileName: string) {
      // Redirect to new method - ignore folderPath, only use fileName
      await this.loadByFileName(fileName);
    },
    async getAll() {
      const { data, error } = await client.GET("/api/Bookmark");
      if (error !== undefined) {
        console.error(error)
      } else {
        this.bookmarks = data as Bookmark[]
      }
    },
    async checkAdminStatus() {
      // @ts-ignore - API schema will be regenerated after backend changes
      const { data, error } = await client.GET("/api/Bookmark/isAdmin" as any);
      if (error !== undefined) {
        console.error(error);
        this.isAdmin = false;
      } else {
        this.isAdmin = data === true;
      }
    },
  },
})