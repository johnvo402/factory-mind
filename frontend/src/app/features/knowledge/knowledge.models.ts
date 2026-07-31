export interface KnowledgeDocument {
  id: string;
  title: string;
  fileName: string;
  contentType: string;
  size: number;
  status: 'uploaded' | 'processing' | 'ready' | 'failed';
  pageCount: number;
  chunkCount: number;
  processingError: string | null;
  createdAt: string;
  processedAt: string | null;
}

export interface KnowledgeSearchResult {
  documentId: string;
  documentTitle: string;
  fileName: string;
  chunkId: string;
  pageNumber: number;
  content: string;
  score: number;
}
