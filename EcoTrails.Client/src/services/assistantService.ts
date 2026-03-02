import apiClient from './apiClient';
import { isCurrentUserAdmin } from './authService';

export interface AssistantChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface AssistantTrailContext {
  id: number;
  name: string;
  location: string;
  difficulty: number;
  durationInHours: number;
  elevationGain: number;
  hasCoordinates: boolean;
  difficultyLevel: string;
  waterSources: boolean;
  maxAltitude: number | null;
  suitableForKids: boolean;
  requiredGear: string[];
}

export interface AssistantKnowledgeChip {
  label: string;
  type: string;
}

export interface AssistantQuickAction {
  id: string;
  label: string;
  value: string;
}

export interface AssistantChatRequest {
  prompt: string;
  sessionId?: string;
  history: AssistantChatMessage[];
  filterSummary?: string;
  favoriteCount: number;
  favoriteTrailIds?: number[];
  maxContextTrails?: number;
  onlyWithCoordinates?: boolean;
}

export interface AssistantChatResponse {
  sessionId: string;
  reply: string;
  model: string;
  provider: string;
  usedTrails: AssistantTrailContext[];
  suggestedAlternatives: AssistantTrailContext[];
  suggestedAlternativeIds: number[];
  knowledgeChips: AssistantKnowledgeChip[];
  quickActions: AssistantQuickAction[];
}

export interface AssistantEnrichRequest {
  limit?: number;
  overwriteExisting?: boolean;
  trailIds?: number[];
}

export interface AssistantEnrichResponse {
  processed: number;
  updated: number;
  failed: number;
  errors: string[];
}

export interface AssistantSessionResponse {
  sessionId: string;
  title: string;
  createdAt: string;
  lastActivityAt: string;
  messageCount: number;
  isOwnedByUser: boolean;
}

export interface AssistantSessionMessageResponse {
  id: number;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

export async function requestAssistantReply(request: AssistantChatRequest): Promise<AssistantChatResponse> {
  const response = await apiClient.post<AssistantChatResponse>('/assistant/chat', request);
  return response.data;
}

export async function createAssistantSession(title?: string): Promise<AssistantSessionResponse> {
  const response = await apiClient.post<AssistantSessionResponse>(
    '/assistant/sessions',
    {
      title,
    },
  );
  return response.data;
}

export async function getMyAssistantSessions(limit = 12): Promise<AssistantSessionResponse[]> {
  const response = await apiClient.get<AssistantSessionResponse[]>('/assistant/sessions/mine', {
    params: { limit },
  });
  return response.data;
}

export async function getAssistantSessionMessages(
  sessionId: string,
  limit = 80,
): Promise<AssistantSessionMessageResponse[]> {
  const response = await apiClient.get<AssistantSessionMessageResponse[]>(
    `/assistant/sessions/${sessionId}/messages`,
    {
      params: { limit },
    },
  );
  return response.data;
}

export async function deleteAssistantSession(sessionId: string): Promise<void> {
  await apiClient.delete(`/assistant/sessions/${sessionId}`);
}

export async function enrichTrailSemanticData(
  request: AssistantEnrichRequest = {},
): Promise<AssistantEnrichResponse> {
  if (!isCurrentUserAdmin()) {
    throw new Error('Admin privileges are required to enrich trail semantic data.');
  }

  const response = await apiClient.post<AssistantEnrichResponse>('/assistant/enrich', request);
  return response.data;
}