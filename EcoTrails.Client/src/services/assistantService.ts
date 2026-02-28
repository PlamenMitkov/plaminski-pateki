import axios from 'axios';
import { getAuthToken } from './authService';

const API_BASE_URL = 'http://127.0.0.1:5218/api';

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

function withAuthHeaders() {
  const token = getAuthToken();
  if (!token) {
    return undefined;
  }

  return { Authorization: `Bearer ${token}` };
}

export async function requestAssistantReply(request: AssistantChatRequest): Promise<AssistantChatResponse> {
  const response = await axios.post<AssistantChatResponse>(`${API_BASE_URL}/assistant/chat`, request, {
    headers: withAuthHeaders(),
  });
  return response.data;
}

export async function createAssistantSession(title?: string): Promise<AssistantSessionResponse> {
  const response = await axios.post<AssistantSessionResponse>(
    `${API_BASE_URL}/assistant/sessions`,
    {
      title,
    },
    {
      headers: withAuthHeaders(),
    },
  );
  return response.data;
}

export async function getMyAssistantSessions(limit = 12): Promise<AssistantSessionResponse[]> {
  const response = await axios.get<AssistantSessionResponse[]>(`${API_BASE_URL}/assistant/sessions/mine`, {
    params: { limit },
    headers: withAuthHeaders(),
  });
  return response.data;
}

export async function getAssistantSessionMessages(
  sessionId: string,
  limit = 80,
): Promise<AssistantSessionMessageResponse[]> {
  const response = await axios.get<AssistantSessionMessageResponse[]>(
    `${API_BASE_URL}/assistant/sessions/${sessionId}/messages`,
    {
      params: { limit },
      headers: withAuthHeaders(),
    },
  );
  return response.data;
}

export async function deleteAssistantSession(sessionId: string): Promise<void> {
  await axios.delete(`${API_BASE_URL}/assistant/sessions/${sessionId}`, {
    headers: withAuthHeaders(),
  });
}

export async function enrichTrailSemanticData(
  request: AssistantEnrichRequest = {},
): Promise<AssistantEnrichResponse> {
  const response = await axios.post<AssistantEnrichResponse>(`${API_BASE_URL}/assistant/enrich`, request, {
    headers: withAuthHeaders(),
  });
  return response.data;
}