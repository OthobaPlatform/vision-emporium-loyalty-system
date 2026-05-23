import { useState, useCallback, useRef } from 'react';
import { apiClient, ApiError } from '../utils/api';
import { useToast } from '../components/Toast';
import { LoadingIndicator } from '../components/LoadingIndicator';
import { getToken } from '../utils/auth';

interface ImportJobStatus {
  jobId: string;
  status: string;
  totalRecords?: number;
  recordsImported?: number;
  recordsRejected?: number;
  recordsSkipped?: number;
  startedAt?: string;
  completedAt?: string;
  errors?: Array<{ row: number; reason: string }>;
}

export function ImportPage() {
  const { showToast } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [jobStatus, setJobStatus] = useState<ImportJobStatus | null>(null);
  const [isPolling, setIsPolling] = useState(false);

  const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/v1';

  function handleDragOver(e: React.DragEvent) {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  }

  function handleDragLeave(e: React.DragEvent) {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    const files = e.dataTransfer.files;
    if (files.length > 0) {
      validateAndSetFile(files[0]);
    }
  }

  function handleFileSelect(e: React.ChangeEvent<HTMLInputElement>) {
    const files = e.target.files;
    if (files && files.length > 0) {
      validateAndSetFile(files[0]);
    }
  }

  function validateAndSetFile(file: File) {
    // Validate file type
    if (!file.name.endsWith('.xlsx')) {
      showToast('error', 'Only .xlsx files are supported');
      return;
    }
    // Validate file size (10MB)
    if (file.size > 10 * 1024 * 1024) {
      showToast('error', 'File size must not exceed 10MB');
      return;
    }
    setSelectedFile(file);
    setJobStatus(null);
  }

  const pollJobStatus = useCallback(
    async (jobId: string) => {
      setIsPolling(true);
      let attempts = 0;
      const maxAttempts = 60; // Poll for up to 5 minutes (5s intervals)

      const poll = async () => {
        try {
          const result = await apiClient.get<ImportJobStatus>(`/ingestion/jobs/${jobId}`);
          setJobStatus(result);

          if (result.status === 'Completed' || result.status === 'Failed') {
            setIsPolling(false);
            return;
          }

          attempts++;
          if (attempts < maxAttempts) {
            setTimeout(poll, 5000);
          } else {
            setIsPolling(false);
          }
        } catch {
          setIsPolling(false);
        }
      };

      await poll();
    },
    []
  );

  async function handleUpload() {
    if (!selectedFile) return;

    setIsUploading(true);
    try {
      const formData = new FormData();
      formData.append('file', selectedFile);

      const token = getToken();
      const response = await fetch(`${API_BASE_URL}/ingestion/upload`, {
        method: 'POST',
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: formData,
      });

      if (!response.ok) {
        const body = await response.json().catch(() => ({ message: 'Upload failed' }));
        throw new ApiError(response.status, response.statusText, body);
      }

      const result = await response.json();
      showToast('success', 'File uploaded successfully. Processing started.');
      setSelectedFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }

      // Start polling for job status
      if (result.jobId) {
        setJobStatus({ jobId: result.jobId, status: 'Processing' });
        pollJobStatus(result.jobId);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        showToast('error', (err.body as { message?: string })?.message || err.message);
      } else {
        showToast('error', 'Failed to upload file');
      }
    } finally {
      setIsUploading(false);
    }
  }

  async function handleDownloadTemplate() {
    try {
      const token = getToken();
      const response = await fetch(`${API_BASE_URL}/ingestion/template`, {
        method: 'GET',
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!response.ok) {
        throw new Error('Failed to download template');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'import-template.xlsx';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch {
      showToast('error', 'Failed to download template');
    }
  }

  function formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  function getStatusColor(status: string): string {
    switch (status) {
      case 'Completed':
        return 'bg-green-100 text-green-800';
      case 'Failed':
        return 'bg-red-100 text-red-800';
      case 'Processing':
        return 'bg-blue-100 text-blue-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Data Import</h1>
        <button
          onClick={handleDownloadTemplate}
          className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
        >
          Download Template
        </button>
      </div>

      {/* Upload Area */}
      <div className="rounded-lg bg-white p-6 shadow border border-gray-200 mb-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Upload Excel File</h2>

        {/* Drag and Drop Zone */}
        <div
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => fileInputRef.current?.click()}
          className={`relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 cursor-pointer transition-colors ${
            isDragging
              ? 'border-blue-400 bg-blue-50'
              : 'border-gray-300 hover:border-gray-400 hover:bg-gray-50'
          }`}
          role="button"
          tabIndex={0}
          aria-label="Upload file area. Click or drag and drop an Excel file here."
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              fileInputRef.current?.click();
            }
          }}
        >
          <svg
            className="h-12 w-12 text-gray-400 mb-3"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={1.5}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
            />
          </svg>
          <p className="text-sm text-gray-600 mb-1">
            <span className="font-medium text-blue-600">Click to browse</span> or drag and drop
          </p>
          <p className="text-xs text-gray-500">
            .xlsx files only, up to 10MB and 100,000 rows
          </p>
          <input
            ref={fileInputRef}
            type="file"
            accept=".xlsx"
            onChange={handleFileSelect}
            className="hidden"
            aria-hidden="true"
          />
        </div>

        {/* Selected File */}
        {selectedFile && (
          <div className="mt-4 flex items-center justify-between rounded-md bg-gray-50 border border-gray-200 p-3">
            <div className="flex items-center gap-3">
              <svg className="h-8 w-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
              </svg>
              <div>
                <p className="text-sm font-medium text-gray-900">{selectedFile.name}</p>
                <p className="text-xs text-gray-500">{formatFileSize(selectedFile.size)}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  setSelectedFile(null);
                  if (fileInputRef.current) fileInputRef.current.value = '';
                }}
                className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
              >
                Remove
              </button>
              <button
                onClick={handleUpload}
                disabled={isUploading}
                className="rounded-md bg-blue-600 px-4 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isUploading ? 'Uploading...' : 'Upload'}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Job Status */}
      {jobStatus && (
        <div className="rounded-lg bg-white p-6 shadow border border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Import Job Status</h2>

          <LoadingIndicator isLoading={isPolling} label="Processing file..." />

          <div className="space-y-4">
            <div className="flex items-center gap-3">
              <span className="text-sm text-gray-500">Job ID:</span>
              <span className="text-sm font-mono text-gray-900">{jobStatus.jobId}</span>
              <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${getStatusColor(jobStatus.status)}`}>
                {jobStatus.status}
              </span>
            </div>

            {(jobStatus.status === 'Completed' || jobStatus.status === 'Failed') && (
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-4 pt-2 border-t border-gray-100">
                <div>
                  <p className="text-sm text-gray-500">Total Records</p>
                  <p className="text-lg font-semibold text-gray-900">
                    {jobStatus.totalRecords ?? 0}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Imported</p>
                  <p className="text-lg font-semibold text-green-700">
                    {jobStatus.recordsImported ?? 0}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Skipped (Duplicates)</p>
                  <p className="text-lg font-semibold text-yellow-700">
                    {jobStatus.recordsSkipped ?? 0}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">Rejected</p>
                  <p className="text-lg font-semibold text-red-700">
                    {jobStatus.recordsRejected ?? 0}
                  </p>
                </div>
              </div>
            )}

            {jobStatus.errors && jobStatus.errors.length > 0 && (
              <div className="pt-2 border-t border-gray-100">
                <p className="text-sm font-medium text-gray-700 mb-2">Rejected Rows:</p>
                <div className="max-h-48 overflow-y-auto rounded-md bg-gray-50 border border-gray-200 p-3">
                  <ul className="space-y-1">
                    {jobStatus.errors.map((err, idx) => (
                      <li key={idx} className="text-xs text-red-700">
                        Row {err.row}: {err.reason}
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
