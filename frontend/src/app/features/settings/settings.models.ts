export interface CompanySettings {
  id: string;
  name: string;
  createdAt: string;
}

export interface UserSettings {
  id: string;
  name: string;
  email: string;
  role: 'Admin' | 'Manager' | 'User';
  isActive: boolean;
  createdAt: string;
}

export interface AiSettings {
  provider: string;
  chatModel: string;
  embeddingModel: string;
  embeddingDimensions: number;
  maximumOutputTokens: number;
  apiKeyConfigured: boolean;
}

export interface CreateUserInput {
  name: string;
  email: string;
  password: string;
  role: UserSettings['role'];
}

export interface UpdateUserInput {
  name: string;
  email: string;
  role: UserSettings['role'];
  isActive: boolean;
}
