import { defineStore } from 'pinia';
import client from "../apiClient"; // Import the global client
import { components } from '../generated/api-schema'; // Import from the OpenAPI schema

export type FilesAndSubdirectories = components['schemas']['FilesAndSubdirectoriesDto'];

/** Timeout for upload (minutes). Large files (e.g. 500MB) may need several minutes on slow links. */
export const UPLOAD_TIMEOUT_MINUTES = 30;
const UPLOAD_TIMEOUT_MS = UPLOAD_TIMEOUT_MINUTES * 60 * 1000;

export const useFileSystemStore = defineStore('fileSystem', {
  state: () => ({
    filesAndSubdirectories: {} as FilesAndSubdirectories,
    loading: false as boolean,
    uploading: false as boolean,
    uploadError: '' as string,
    /** Upload progress 0-100. Only meaningful while uploading. */
    uploadProgress: 0 as number,
  }),

  actions: {
    async loadFiles() {
      // Set loading state to true
      this.loading = true;

      try {
        // Fetch files from the data directory only
        const { data, error } = await client.GET('/api/FileSystem');

        if (error) {
          console.error(error);
        } else if (data) {
          // Set the received data to the store state
          this.filesAndSubdirectories = data as FilesAndSubdirectories;
        }
      } catch (err) {
        console.error('Error loading files:', err);
      } finally {
        // Set loading state to false after the operation
        this.loading = false;
      }
    },
    async uploadFile(file: File) {
      return this.uploadFiles([file]);
    },
    async uploadFiles(files: File[], onProgress?: (percent: number) => void): Promise<unknown> {
      this.uploading = true;
      this.uploadError = '';
      this.uploadProgress = 0;

      const formData = new FormData();
      files.forEach(file => {
        formData.append('files', file);
      });

      const adminKey = localStorage.getItem('adminKey');
      const baseUrl = import.meta.env.VITE_API_BASE_URL ||
        (import.meta.env.PROD ? "" : "http://localhost:5054/");

      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        let timeoutId: ReturnType<typeof setTimeout>;

        const cleanup = () => {
          clearTimeout(timeoutId);
          this.uploading = false;
          this.uploadProgress = 0;
        };

        timeoutId = setTimeout(() => {
          xhr.abort();
          this.uploadError = 'Upload timed out. Large files (e.g. 500MB) can take several minutes—please try again or check your connection.';
          cleanup();
          reject(new Error(this.uploadError));
        }, UPLOAD_TIMEOUT_MS);

        xhr.upload.addEventListener('progress', (e) => {
          if (e.lengthComputable) {
            const percent = Math.round((e.loaded / e.total) * 100);
            this.uploadProgress = percent;
            onProgress?.(percent);
          }
        });

        xhr.addEventListener('load', async () => {
          clearTimeout(timeoutId);
          try {
            if (xhr.status >= 200 && xhr.status < 300) {
              this.uploadProgress = 100;
              const json = JSON.parse(xhr.responseText || '{}');
              await this.loadFiles();
              cleanup();
              resolve(json);
            } else {
              let message = xhr.statusText;
              try {
                const err = JSON.parse(xhr.responseText || '{}');
                message = err.message || message;
              } catch {
                if (xhr.status === 413) message = 'File too large. Maximum size is 1 GB per file.';
                else if (xhr.status === 401) message = 'Admin key required to upload.';
              }
              this.uploadError = message;
              cleanup();
              reject(new Error(message));
            }
          } catch (err) {
            this.uploadError = err instanceof Error ? err.message : 'Upload failed';
            cleanup();
            reject(err);
          }
        });

        xhr.addEventListener('error', () => {
          clearTimeout(timeoutId);
          this.uploadError = 'Network error. Check your connection and try again.';
          cleanup();
          reject(new Error(this.uploadError));
        });

        xhr.addEventListener('abort', () => {
          if (!this.uploadError) this.uploadError = 'Upload was cancelled or timed out.';
          cleanup();
          reject(new Error(this.uploadError));
        });

        xhr.open('POST', `${baseUrl}api/FileSystem/upload`);
        if (adminKey) xhr.setRequestHeader('X-Admin-Key', adminKey);
        xhr.send(formData);
      });
    },
    async deleteFile(fileName: string) {
      try {
        // Get admin key from localStorage
        const adminKey = localStorage.getItem('adminKey');
        const baseUrl = import.meta.env.VITE_API_BASE_URL ||
          (import.meta.env.PROD ? "" : "http://localhost:5054/");
        
        const headers: Record<string, string> = {};
        if (adminKey) {
          headers['X-Admin-Key'] = adminKey;
        }

        const response = await fetch(`${baseUrl}api/FileSystem/${encodeURIComponent(fileName)}`, {
          method: 'DELETE',
          headers: headers,
        });

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({ message: response.statusText }));
          throw new Error(errorData.message || `Delete failed: ${response.statusText}`);
        }

        // Reload file list after successful delete
        await this.loadFiles();
        return await response.json();
      } catch (err) {
        console.error('Error deleting file:', err);
        throw err;
      }
    },
    async initialize() {
      // Load files from data directory
      await this.loadFiles();
    },
  },
});
