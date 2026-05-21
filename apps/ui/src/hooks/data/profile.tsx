import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { useGet, usePost, type Response } from '@/hooks/data/core';
export interface ProfileItem {
  name: string;
  accountName: string;
  displayName: string;
  role: 'user' | 'administrator';
  canCreateApplications: boolean;
  language: string;
  timezone: string;
  location: string;
  memberOf: string[];
  metadata: { [key: string]: string | number };
}

export interface UpdateProfileItem {
  displayName: string;
  timezone?: string;
  location?: string;
  language?: string;
}

export interface ProfileType {
  profile: ProfileItem;
  loadProfile: () => Promise<ProfileItem | undefined>;
  saveProfile: (profile: UpdateProfileItem) => Promise<Response<ProfileItem>>;
  isFetchingProfile: boolean;
  isSavingProfile: boolean;
  loadError?: string;
  saveError?: string;
}

const ProfileContext = createContext<ProfileType>({} as any);

export function ProfileProvider({ children }: { children: React.ReactNode }) {
  const { data, fetchAsync, isFetching, error } = useGet<ProfileItem>(`/api/profile`);
  const { postAsync, isFetching: isSavingProfile, error: saveError } = usePost<ProfileItem>(`/api/profile`);

  const [profile, setProfile] = useState<ProfileItem | undefined>({} as any);

  const loadProfile = useCallback(async () => {
    const { data: profile } = await fetchAsync('');
    setProfile(profile);
    return profile;
  }, [fetchAsync]);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  const saveProfile = async (profile: UpdateProfileItem) => {
    const response = await postAsync(JSON.stringify(profile), 'application/json');
    if (!response.error) {
      setProfile(data);
    }
    return response;
  };

  return (
    <ProfileContext.Provider
      value={{
        profile: profile!,
        loadProfile: loadProfile,
        isFetchingProfile: isFetching,
        isSavingProfile: isSavingProfile,
        saveProfile: saveProfile,
        loadError: error,
        saveError: saveError
      }}>
      {children}
    </ProfileContext.Provider>
  );
}

export const useProfile = () => {
  return useContext(ProfileContext);
};
