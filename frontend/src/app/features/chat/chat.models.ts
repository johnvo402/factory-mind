export interface Conversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChatCitation {
  referenceNumber: number;
  documentId: string;
  chunkId: string;
  documentTitle: string;
  fileName: string;
  pageNumber: number;
  excerpt: string;
  score: number;
}

export interface ChatBusinessEvidence {
  referenceNumber: number;
  entityId: string;
  entityType: string;
  title: string;
  detail: string;
}

export interface ChatMessage {
  id: string;
  role: 'system' | 'user' | 'assistant';
  content: string;
  createdAt: string;
  citations: ChatCitation[];
  businessEvidence: ChatBusinessEvidence[];
}

export interface AiActionProposalSummary {
  productionOrderNumber: string;
  product: string;
  quantity: number;
  bomRevision: number;
  routingRevision: number;
}

export type AiActionProposalStatus =
  | 'pending' | 'confirmed' | 'succeeded' | 'failed' | 'cancelled' | 'expired' | 'stale';

export interface AiActionProposal {
  proposalId: string;
  actionType: 'release_production_order';
  status: AiActionProposalStatus;
  title: string;
  summary: AiActionProposalSummary;
  expiresAt: string;
  failureCode: string | null;
}

export type ChatStreamEvent =
  | { type: 'conversation'; conversationId: string }
  | { type: 'token'; content: string }
  | { type: 'business-evidence'; businessEvidence: ChatBusinessEvidence[] }
  | { type: 'citations'; citations: ChatCitation[] }
  | { type: 'ai-action-proposal'; proposal: AiActionProposal }
  | { type: 'done' };
