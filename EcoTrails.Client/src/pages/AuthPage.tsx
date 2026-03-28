import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Lock, LogOut, Save, Shield, Sparkles, User } from 'lucide-react';
import axios from 'axios';
import { 
  login, 
  register, 
  updateProfile,
  changePassword,
  type UpdateProfileRequest,
  type ChangePasswordRequest,
} from '../services/authService';
import { useAuthCapabilities } from '../hooks/useAuthCapabilities';
import { useFavorites } from '../hooks/useFavorites';
import {
  createCommunityPost,
  getMyCommunityPosts,
  updateCommunityPost,
  deleteCommunityPost,
  type CommunityPostResponse,
} from '../services/communityService';
import {
  deleteAssistantSession,
  getMyAssistantSessions,
  type AssistantSessionResponse,
} from '../services/assistantService';
import {
  getTrailDataQuality,
  triggerManualReEnrich,
  getPendingTrailProposals,
  approveTrailProposal,
  type TrailDataQualityResponse,
} from '../services/adminService';
import apiClient from '../services/apiClient';
import '../App.css';

const ASSISTANT_SESSION_STORAGE_KEY = 'ecotrails:assistantSessionId';
const PROFILE_PREFERENCES_STORAGE_KEY = 'ecotrails:profilePreferences';

type ProfilePreferences = {
  preferredName: string;
  bio: string;
};

type PostSortOption = 'newest' | 'oldest' | 'title';

function parseProfilePreferences(): ProfilePreferences {
  try {
    const raw = localStorage.getItem(PROFILE_PREFERENCES_STORAGE_KEY);
    if (!raw) {
      return { preferredName: '', bio: '' };
    }

    const parsed = JSON.parse(raw) as Partial<ProfilePreferences>;
    return {
      preferredName: typeof parsed.preferredName === 'string' ? parsed.preferredName : '',
      bio: typeof parsed.bio === 'string' ? parsed.bio : '',
    };
  } catch {
    return { preferredName: '', bio: '' };
  }
}

function formatDateTime(dateIso: string): string {
  return new Date(dateIso).toLocaleString('bg-BG');
}

function AuthPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const [postTitle, setPostTitle] = useState('');
  const [postContent, setPostContent] = useState('');
  const [postTrailId, setPostTrailId] = useState('');
  const [postImages, setPostImages] = useState<File[]>([]);
  const [postIsTrailProposal, setPostIsTrailProposal] = useState(false);
  const [isPosting, setIsPosting] = useState(false);
  const [postError, setPostError] = useState('');
  const [postNotice, setPostNotice] = useState('');
  const [myPosts, setMyPosts] = useState<CommunityPostResponse[]>([]);
  const [isLoadingPosts, setIsLoadingPosts] = useState(false);
  const [postsFilter, setPostsFilter] = useState('');
  const [postSort, setPostSort] = useState<PostSortOption>('newest');
  const [draftSourcePostId, setDraftSourcePostId] = useState<number | null>(null);

  const [assistantSessions, setAssistantSessions] = useState<AssistantSessionResponse[]>([]);
  const [isLoadingAssistantSessions, setIsLoadingAssistantSessions] = useState(false);
  const [assistantSessionError, setAssistantSessionError] = useState('');

  const [profilePreferences, setProfilePreferences] = useState<ProfilePreferences>(() => parseProfilePreferences());
  const [preferencesNotice, setPreferencesNotice] = useState('');

  const [quality, setQuality] = useState<TrailDataQualityResponse | null>(null);
  const [qualityError, setQualityError] = useState('');
  const [qualityNotice, setQualityNotice] = useState('');
  const [isRefreshingQuality, setIsRefreshingQuality] = useState(false);
  const [isManualReEnriching, setIsManualReEnriching] = useState(false);
  const [pendingTrailProposals, setPendingTrailProposals] = useState<CommunityPostResponse[]>([]);
  const [isLoadingPendingTrailProposals, setIsLoadingPendingTrailProposals] = useState(false);
  const [pendingTrailProposalsError, setPendingTrailProposalsError] = useState('');
  const [approvingPostId, setApprovingPostId] = useState<number | null>(null);

  const [isExportingFavorites, setIsExportingFavorites] = useState(false);

  // Profile Edit Modal
  const [showEditProfileModal, setShowEditProfileModal] = useState(false);
  const [editProfileEmail, setEditProfileEmail] = useState('');
  const [editProfileUserName, setEditProfileUserName] = useState('');
  const [editProfilePhoneNumber, setEditProfilePhoneNumber] = useState('');
  const [isUpdatingProfile, setIsUpdatingProfile] = useState(false);
  const [profileUpdateError, setProfileUpdateError] = useState('');
  const [profileUpdateNotice, setProfileUpdateNotice] = useState('');

  // Password Change Modal
  const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [newPasswordConfirm, setNewPasswordConfirm] = useState('');
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [passwordChangeError, setPasswordChangeError] = useState('');
  const [passwordChangeNotice, setPasswordChangeNotice] = useState('');

  // Post Edit/Delete Modal
  const [editingPostId, setEditingPostId] = useState<number | null>(null);
  const [editPostTitle, setEditPostTitle] = useState('');
  const [editPostContent, setEditPostContent] = useState('');
  const [editPostTrailId, setEditPostTrailId] = useState('');
  const [isUpdatingPost, setIsUpdatingPost] = useState(false);
  const [postEditError, setPostEditError] = useState('');

  const { authUser, sessionInfo, isAdmin, refreshSession, clearAuth } = useAuthCapabilities();
  const sessionExpired = searchParams.get('reason') === 'session-expired';

  const {
    favoriteIds,
    hasToken,
    hasPendingCloudSync,
    syncFavoritesToCloud,
    isSyncing,
    lastSyncError,
  } = useFavorites();

  useEffect(() => {
    void refreshSession();
  }, [refreshSession]);

  const loadMyPosts = async () => {
    if (!authUser) {
      setMyPosts([]);
      return;
    }

    try {
      setIsLoadingPosts(true);
      setPostError('');
      const posts = await getMyCommunityPosts();
      setMyPosts(posts);
    } catch (requestError) {
      console.error('Грешка при зареждане на публикациите:', requestError);
      setPostError('Неуспешно зареждане на публикациите.');
    } finally {
      setIsLoadingPosts(false);
    }
  };

  const loadAssistantSessions = async () => {
    if (!authUser) {
      setAssistantSessions([]);
      return;
    }

    try {
      setIsLoadingAssistantSessions(true);
      setAssistantSessionError('');
      const sessions = await getMyAssistantSessions(12);
      setAssistantSessions(sessions);
    } catch (requestError) {
      console.error('Грешка при зареждане на AI сесиите:', requestError);
      setAssistantSessionError('Неуспешно зареждане на AI сесиите.');
    } finally {
      setIsLoadingAssistantSessions(false);
    }
  };

  const loadDataQuality = async () => {
    if (!isAdmin) {
      setQuality(null);
      return;
    }

    try {
      setIsRefreshingQuality(true);
      setQualityError('');
      const data = await getTrailDataQuality();
      setQuality(data);
    } catch (requestError) {
      console.error('Грешка при зареждане на data quality:', requestError);
      setQualityError('Неуспешно зареждане на data quality метрики.');
    } finally {
      setIsRefreshingQuality(false);
    }
  };

  const loadPendingTrailProposals = async () => {
    if (!isAdmin) {
      setPendingTrailProposals([]);
      return;
    }

    try {
      setIsLoadingPendingTrailProposals(true);
      setPendingTrailProposalsError('');
      const proposals = await getPendingTrailProposals();
      setPendingTrailProposals(proposals);
    } catch (requestError) {
      console.error('Грешка при зареждане на предложенията за нова пътека:', requestError);
      setPendingTrailProposalsError('Неуспешно зареждане на предложенията за нова пътека.');
    } finally {
      setIsLoadingPendingTrailProposals(false);
    }
  };

  useEffect(() => {
    void loadMyPosts();
    void loadAssistantSessions();
  }, [authUser]);

  useEffect(() => {
    void loadDataQuality();
    void loadPendingTrailProposals();
  }, [isAdmin]);

  const submit = async () => {
    try {
      setIsSubmitting(true);
      setError('');

      if (!email.trim()) {
        setError('Въведи валиден имейл.');
        return;
      }

      if (!password.trim()) {
        setError('Въведи парола.');
        return;
      }

      if (mode === 'register') {
        if (password.length < 6) {
          setError('Паролата трябва да е поне 6 символа.');
          return;
        }

        if (!/\d/.test(password)) {
          setError('Паролата трябва да съдържа поне една цифра.');
          return;
        }
      }

      if (mode === 'login') {
        await login(email, password);
      } else {
        await register(email, password);
      }

      await refreshSession();
      navigate('/auth', { replace: true });
    } catch (requestError) {
      console.error('Auth error:', requestError);

      if (axios.isAxiosError(requestError)) {
        const status = requestError.response?.status;
        const responseData = requestError.response?.data;

        if (Array.isArray(responseData) && responseData.length > 0) {
          setError(responseData.join(' '));
        } else if (typeof responseData === 'string' && responseData.trim().length > 0) {
          setError(responseData);
        } else if (status === 409) {
          setError('Потребител с този имейл вече съществува.');
        } else if (status === 429) {
          setError('Прекалено много опити. Изчакай малко и опитай отново.');
        } else {
          setError('Неуспешна автентикация. Провери данните и опитай отново.');
        }
      } else {
        setError('Неуспешна автентикация. Провери данните и опитай отново.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitPost = async () => {
    if (!authUser) {
      setPostError('Трябва да си влязъл, за да публикуваш.');
      return;
    }

    try {
      setPostError('');
      setPostNotice('');
      setIsPosting(true);

      const trailIdNumeric = Number(postTrailId);
      await createCommunityPost({
        title: postTitle,
        content: postContent,
        trailId: Number.isInteger(trailIdNumeric) && trailIdNumeric > 0 ? trailIdNumeric : undefined,
        postType: postIsTrailProposal ? 'TrailProposal' : undefined,
        images: postImages,
      });

      const hadSourcePost = draftSourcePostId !== null;
      setPostTitle('');
      setPostContent('');
      setPostTrailId('');
      setPostImages([]);
      setPostIsTrailProposal(false);
      setDraftSourcePostId(null);
      setPostNotice(
        hadSourcePost
          ? 'Публикувана е нова версия на поста. В стария пост няма редакция (backend фаза 2).'
          : 'Публикацията е добавена успешно.',
      );
      await loadMyPosts();
      if (isAdmin) {
        await loadPendingTrailProposals();
      }
    } catch (requestError) {
      console.error('Грешка при създаване на публикация:', requestError);

      if (axios.isAxiosError(requestError) && typeof requestError.response?.data === 'string') {
        setPostError(requestError.response.data);
      } else {
        setPostError('Неуспешно създаване на публикация. Провери данните и снимките.');
      }
    } finally {
      setIsPosting(false);
    }
  };

  const runManualReEnrich = async () => {
    try {
      setIsManualReEnriching(true);
      setQualityError('');
      setQualityNotice('');
      await triggerManualReEnrich();
      setQualityNotice('Ръчното re-enrich изпълнение приключи за warmup batch.');
      await loadDataQuality();
    } catch (requestError) {
      console.error('Грешка при ръчно re-enrich:', requestError);
      setQualityError('Неуспешно ръчно re-enrich изпълнение.');
    } finally {
      setIsManualReEnriching(false);
    }
  };

  const submitApproveProposal = async (postId: number) => {
    try {
      setApprovingPostId(postId);
      setPendingTrailProposalsError('');
      await approveTrailProposal(postId);
      setQualityNotice('Предложението е одобрено и е създадена нова пътека.');
      await Promise.all([loadPendingTrailProposals(), loadDataQuality()]);
    } catch (requestError) {
      console.error('Грешка при одобрение на предложение за пътека:', requestError);

      if (axios.isAxiosError(requestError) && typeof requestError.response?.data === 'string') {
        setPendingTrailProposalsError(requestError.response.data);
      } else {
        setPendingTrailProposalsError('Неуспешно одобрение на предложението.');
      }
    } finally {
      setApprovingPostId(null);
    }
  };

  const openAssistantSessionInHome = (sessionId: string) => {
    localStorage.setItem(ASSISTANT_SESSION_STORAGE_KEY, sessionId);
    navigate('/?tab=assistant');
  };

  const removeAssistantSessionFromProfile = async (sessionId: string) => {
    try {
      await deleteAssistantSession(sessionId);
      if (localStorage.getItem(ASSISTANT_SESSION_STORAGE_KEY) === sessionId) {
        localStorage.removeItem(ASSISTANT_SESSION_STORAGE_KEY);
      }

      setAssistantSessions((current) => current.filter((session) => session.sessionId !== sessionId));
    } catch (requestError) {
      console.error('Грешка при изтриване на AI сесия:', requestError);
      setAssistantSessionError('Неуспешно изтриване на сесията.');
    }
  };

  const openEditProfileModal = () => {
    if (authUser) {
      setEditProfileEmail(authUser.email);
      setEditProfileUserName(sessionInfo?.userName || '');
      setEditProfilePhoneNumber(sessionInfo?.phoneNumber || '');
      setShowEditProfileModal(true);
      setProfileUpdateError('');
      setProfileUpdateNotice('');
    }
  };

  const submitEditProfile = async () => {
    try {
      setIsUpdatingProfile(true);
      setProfileUpdateError('');
      setProfileUpdateNotice('');

      const updateReq: UpdateProfileRequest = {
        email: editProfileEmail.trim() || undefined,
        userName: editProfileUserName.trim() || undefined,
        phoneNumber: editProfilePhoneNumber.trim() || undefined,
      };

      await updateProfile(updateReq);
      await refreshSession();
      setShowEditProfileModal(false);
      setProfileUpdateNotice('Профилът е актуализиран успешно.');
    } catch (requestError) {
      console.error('Грешка при актуализация на профила:', requestError);

      if (axios.isAxiosError(requestError)) {
        const responseData = requestError.response?.data;
        if (Array.isArray(responseData) && responseData.length > 0) {
          setProfileUpdateError(responseData.join(' '));
        } else if (typeof responseData === 'string' && responseData.trim().length > 0) {
          setProfileUpdateError(responseData);
        } else {
          setProfileUpdateError('Неуспешна актуализация. Провери данните и опитай отново.');
        }
      } else {
        setProfileUpdateError('Неуспешна актуализация. Провери данните и опитай отново.');
      }
    } finally {
      setIsUpdatingProfile(false);
    }
  };

  const openChangePasswordModal = () => {
    setShowChangePasswordModal(true);
    setCurrentPassword('');
    setNewPassword('');
    setNewPasswordConfirm('');
    setPasswordChangeError('');
    setPasswordChangeNotice('');
  };

  const submitChangePassword = async () => {
    if (!newPassword.trim()) {
      setPasswordChangeError('Въведи нова парола.');
      return;
    }

    if (newPassword.length < 6) {
      setPasswordChangeError('Новата парола трябва да е поне 6 символа.');
      return;
    }

    if (!/\d/.test(newPassword)) {
      setPasswordChangeError('Новата парола трябва да съдържа поне една цифра.');
      return;
    }

    if (newPassword !== newPasswordConfirm) {
      setPasswordChangeError('Паролите не съвпадат.');
      return;
    }

    try {
      setIsChangingPassword(true);
      setPasswordChangeError('');
      setPasswordChangeNotice('');

      const changeReq: ChangePasswordRequest = {
        currentPassword: currentPassword.trim(),
        newPassword: newPassword.trim(),
      };

      await changePassword(changeReq);
      setShowChangePasswordModal(false);
      setPasswordChangeNotice('Паролата е променена успешно.');
    } catch (requestError) {
      console.error('Грешка при смяна на парола:', requestError);

      if (axios.isAxiosError(requestError)) {
        const status = requestError.response?.status;
        const responseData = requestError.response?.data;

        if (status === 400) {
          setPasswordChangeError('Текущата парола е грешна или новата парола не е валидна.');
        } else if (Array.isArray(responseData) && responseData.length > 0) {
          setPasswordChangeError(responseData.join(' '));
        } else if (typeof responseData === 'string' && responseData.trim().length > 0) {
          setPasswordChangeError(responseData);
        } else {
          setPasswordChangeError('Неуспешна смяна на парола. Опитай отново.');
        }
      } else {
        setPasswordChangeError('Неуспешна смяна на парола. Опитай отново.');
      }
    } finally {
      setIsChangingPassword(false);
    }
  };

  const openEditPostModal = (post: CommunityPostResponse) => {
    setEditingPostId(post.id);
    setEditPostTitle(post.title);
    setEditPostContent(post.content);
    setEditPostTrailId(post.trailId ? String(post.trailId) : '');
    setIsUpdatingPost(false);
    setPostEditError('');
  };

  const submitEditPost = async () => {
    if (!editingPostId) {
      return;
    }

    try {
      setIsUpdatingPost(true);
      setPostEditError('');

      const trailIdNumeric = Number(editPostTrailId);
      await updateCommunityPost(editingPostId, {
        title: editPostTitle,
        content: editPostContent,
        trailId: Number.isInteger(trailIdNumeric) && trailIdNumeric > 0 ? trailIdNumeric : undefined,
      });

      setEditingPostId(null);
      setEditPostTitle('');
      setEditPostContent('');
      setEditPostTrailId('');
      await loadMyPosts();
      setPostNotice('Публикацията е обновена успешно.');
    } catch (requestError) {
      console.error('Грешка при редакция на публикация:', requestError);

      if (axios.isAxiosError(requestError) && typeof requestError.response?.data === 'string') {
        setPostEditError(requestError.response.data);
      } else {
        setPostEditError('Неуспешно редактиране на публикация.');
      }
    } finally {
      setIsUpdatingPost(false);
    }
  };

  const submitDeletePost = async (postId: number) => {
    if (!confirm('Сигурен ли си, че искаш да изтриеш тази публикация?')) {
      return;
    }

    try {
      await deleteCommunityPost(postId);
      await loadMyPosts();
      setPostNotice('Публикацията е изтрита успешно.');
    } catch (requestError) {
      console.error('Грешка при изтриване на публикация:', requestError);
      setPostError('Неуспешно изтриване на публикация.');
    }
  };

  const saveProfilePreferences = () => {
    localStorage.setItem(PROFILE_PREFERENCES_STORAGE_KEY, JSON.stringify(profilePreferences));
    setPreferencesNotice('Локалните профилни данни са запазени за този браузър.');
  };

  const exportFavoritesSnapshot = async () => {
    try {
      setIsExportingFavorites(true);
      setPostError('');

      const ids = favoriteIds.length > 0 ? favoriteIds.join(',') : undefined;
      const response = await apiClient.get('/trails/export', {
        params: { ids },
      });

      const payload = {
        exportedAt: new Date().toISOString(),
        exportType: 'profile-favorites-snapshot',
        favoriteTrailCount: favoriteIds.length,
        trails: response.data,
      };

      const blob = new Blob([JSON.stringify(payload, null, 2)], {
        type: 'application/json;charset=utf-8',
      });

      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `ecotrails-profile-favorites-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
    } catch (exportError) {
      console.error('Грешка при експорт на favorite snapshot:', exportError);
      setPostError('Неуспешен експорт.');
    } finally {
      setIsExportingFavorites(false);
    }
  };

  const sortedAndFilteredPosts = useMemo(() => {
    const query = postsFilter.trim().toLowerCase();
    const filtered = query
      ? myPosts.filter((post) => `${post.title} ${post.content} ${post.trailName}`.toLowerCase().includes(query))
      : myPosts;

    const sorted = [...filtered];
    if (postSort === 'newest') {
      sorted.sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());
    } else if (postSort === 'oldest') {
      sorted.sort((a, b) => new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime());
    } else {
      sorted.sort((a, b) => a.title.localeCompare(b.title, 'bg'));
    }

    return sorted;
  }, [myPosts, postSort, postsFilter]);

  const postStats = useMemo(() => {
    const total = myPosts.length;
    const withImages = myPosts.filter((post) => post.imageUrls.length > 0).length;
    const linkedToTrail = myPosts.filter((post) => !!post.trailId).length;

    return { total, withImages, linkedToTrail };
  }, [myPosts]);

  const roleLabels = useMemo(() => {
    if (!sessionInfo || sessionInfo.roles.length === 0) {
      return ['Потребител'];
    }

    return sessionInfo.roles;
  }, [sessionInfo]);

  const lastActivityLabel = useMemo(() => {
    const timestamps: number[] = [];

    if (myPosts.length > 0) {
      const newestPost = Math.max(...myPosts.map((post) => new Date(post.createdAtUtc).getTime()));
      timestamps.push(newestPost);
    }

    if (assistantSessions.length > 0) {
      const newestSession = Math.max(...assistantSessions.map((session) => new Date(session.lastActivityAt).getTime()));
      timestamps.push(newestSession);
    }

    if (timestamps.length === 0) {
      return 'Няма активност засега';
    }

    return formatDateTime(new Date(Math.max(...timestamps)).toISOString());
  }, [assistantSessions, myPosts]);

  if (authUser) {
    return (
      <div className="app-container auth-page profile-hub-page">
        <div className="auth-card profile-hub-card">
          <div className="profile-hub-header">
            <h1 className="app-title">Профил</h1>
            <button type="button" className="secondary-btn" onClick={clearAuth}>
              <LogOut size={16} />
              Изход
            </button>
          </div>

          <div className="profile-grid">
            <section className="profile-card profile-overview-card">
              <h3>
                <User size={16} />
                Profile Overview
              </h3>
              <p className="status-text">Имейл: {authUser.email}</p>
              <p className="status-text">Последна активност: {lastActivityLabel}</p>
              <div className="profile-role-list">
                {roleLabels.map((role) => (
                  <span key={role} className="assistant-chip assistant-chip-positive">
                    {role}
                  </span>
                ))}
              </div>

              {profileUpdateNotice && <p className="status-text">{profileUpdateNotice}</p>}

              <label className="auth-label" htmlFor="profile-preferred-name">
                Име за показване (локално)
              </label>
              <input
                id="profile-preferred-name"
                className="search-input auth-input"
                type="text"
                value={profilePreferences.preferredName}
                onChange={(event) =>
                  setProfilePreferences((current) => ({ ...current, preferredName: event.target.value }))
                }
                placeholder="Напр. Иван Петров"
              />

              <label className="auth-label" htmlFor="profile-bio">
                Кратко био (локално)
              </label>
              <textarea
                id="profile-bio"
                className="search-input auth-input"
                value={profilePreferences.bio}
                onChange={(event) => setProfilePreferences((current) => ({ ...current, bio: event.target.value }))}
                placeholder="Планински преходи, фотография, семейни маршрути..."
                rows={3}
              />

              {preferencesNotice && <p className="status-text">{preferencesNotice}</p>}
              <div className="profile-actions-row">
                <button type="button" className="secondary-btn" onClick={saveProfilePreferences}>
                  <Save size={16} />
                  Запази локални данни
                </button>
                <button type="button" className="primary-btn" onClick={openEditProfileModal}>
                  Редактирай профил
                </button>
              </div>

              <button
                type="button"
                className="secondary-btn"
                onClick={openChangePasswordModal}
                style={{ marginTop: '10px', width: '100%' }}
              >
                <Lock size={16} />
                Смени парола
              </button>
            </section>

            <section className="profile-card profile-quick-actions-card">
              <h3>
                <Sparkles size={16} />
                Quick Actions
              </h3>
              <p className="status-text">Любими: {favoriteIds.length}</p>
              {hasPendingCloudSync && <p className="status-text">Има несинхронизирани любими.</p>}
              {lastSyncError && <p className="status-text error">{lastSyncError}</p>}
              <div className="profile-actions-row">
                <button
                  type="button"
                  className="secondary-btn"
                  disabled={!hasToken || isSyncing}
                  onClick={() => void syncFavoritesToCloud()}
                >
                  {isSyncing ? 'Синхронизация...' : 'Sync любими'}
                </button>
                <Link className="trail-link" to="/?tab=favorites">
                  Отвори Favorites
                </Link>
              </div>

              <div className="profile-actions-row">
                <button
                  type="button"
                  className="primary-btn"
                  onClick={() => void exportFavoritesSnapshot()}
                  disabled={isExportingFavorites}
                >
                  {isExportingFavorites ? 'Експорт...' : 'Експорт Favorites snapshot'}
                </button>
                <Link className="trail-link" to="/?tab=assistant">
                  Отиди в AI асистент
                </Link>
              </div>
            </section>
          </div>

          <section className="profile-card profile-assistant-card">
            <h3>AI сесии</h3>
            {assistantSessionError && <p className="status-text error">{assistantSessionError}</p>}
            {isLoadingAssistantSessions ? (
              <p className="status-text">Зареждане на сесиите...</p>
            ) : assistantSessions.length === 0 ? (
              <p className="status-text">Няма записани AI сесии.</p>
            ) : (
              <div className="assistant-session-list">
                {assistantSessions.map((session) => (
                  <div key={session.sessionId} className="assistant-session-item">
                    <button
                      type="button"
                      className="assistant-session-open"
                      onClick={() => openAssistantSessionInHome(session.sessionId)}
                    >
                      <span>{session.title || 'Нова AI сесия'}</span>
                      <small>
                        {session.messageCount} съобщения • {formatDateTime(session.lastActivityAt)}
                      </small>
                    </button>
                    <button
                      type="button"
                      className="assistant-session-delete"
                      onClick={() => void removeAssistantSessionFromProfile(session.sessionId)}
                      aria-label="Изтрий AI сесия"
                      title="Изтрий AI сесия"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="profile-card profile-post-form">
            <h3>Нова публикация за пътека</h3>

            {draftSourcePostId && (
              <p className="status-text">Редакция като нова версия на пост #{draftSourcePostId}</p>
            )}

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void submitPost();
              }}
            >
              <label className="auth-label" htmlFor="post-title">
                Заглавие
              </label>
              <input
                id="post-title"
                className="search-input auth-input"
                type="text"
                value={postTitle}
                onChange={(event) => setPostTitle(event.target.value)}
                placeholder="Заглавие (напр. Състояние на маршрута)"
              />

              <label className="auth-label" htmlFor="post-content">
                Съдържание
              </label>
              <textarea
                id="post-content"
                className="search-input auth-input"
                value={postContent}
                onChange={(event) => setPostContent(event.target.value)}
                placeholder="Опиши достъп, маркировка, опасности, условия..."
                rows={5}
              />

              <label className="auth-label" htmlFor="post-trail-id">
                Trail ID (по избор)
              </label>
              <input
                id="post-trail-id"
                className="search-input auth-input"
                type="number"
                value={postTrailId}
                onChange={(event) => setPostTrailId(event.target.value)}
                placeholder="Trail ID"
              />

              <label className="auth-label" htmlFor="post-images">
                Снимки (до 4)
              </label>
              <input
                id="post-images"
                className="search-input auth-input"
                type="file"
                multiple
                accept="image/jpeg,image/png,image/webp"
                onChange={(event) => setPostImages(Array.from(event.target.files ?? []))}
              />

              <label className="auth-label" htmlFor="post-is-proposal">
                <input
                  id="post-is-proposal"
                  type="checkbox"
                  checked={postIsTrailProposal}
                  onChange={(event) => setPostIsTrailProposal(event.target.checked)}
                  style={{ marginRight: '8px' }}
                />
                Това е предложение за нова екопътека
              </label>

              {postError && <p className="status-text error">{postError}</p>}
              {postNotice && <p className="status-text">{postNotice}</p>}

              <div className="profile-actions-row">
                <button type="submit" className="primary-btn auth-submit" disabled={isPosting}>
                  {isPosting ? 'Публикуване...' : 'Публикувай'}
                </button>
                {draftSourcePostId && (
                  <button
                    type="button"
                    className="secondary-btn"
                    onClick={() => {
                      setDraftSourcePostId(null);
                      setPostTitle('');
                      setPostContent('');
                      setPostTrailId('');
                      setPostImages([]);
                      setPostIsTrailProposal(false);
                    }}
                  >
                    Изчисти чернова
                  </button>
                )}
              </div>
            </form>
          </section>

          <section className="profile-card profile-post-list">
            <h3>Моите публикации</h3>
            <div className="profile-post-toolbar">
              <input
                className="search-input auth-input"
                type="text"
                value={postsFilter}
                onChange={(event) => setPostsFilter(event.target.value)}
                placeholder="Филтър по заглавие/текст"
              />
              <select
                className="select-input"
                value={postSort}
                onChange={(event) => setPostSort(event.target.value as PostSortOption)}
              >
                <option value="newest">Най-нови</option>
                <option value="oldest">Най-стари</option>
                <option value="title">По заглавие</option>
              </select>
            </div>

            <div className="profile-stats-grid">
              <div className="chart-card">
                <h4>Общо публикации</h4>
                <p>{postStats.total}</p>
              </div>
              <div className="chart-card">
                <h4>Със снимки</h4>
                <p>{postStats.withImages}</p>
              </div>
              <div className="chart-card">
                <h4>Свързани с пътека</h4>
                <p>{postStats.linkedToTrail}</p>
              </div>
            </div>

            {isLoadingPosts ? (
              <p className="status-text">Зареждане...</p>
            ) : sortedAndFilteredPosts.length === 0 ? (
              <p className="status-text">Няма съвпадащи публикации.</p>
            ) : (
              sortedAndFilteredPosts.map((post) => (
                <article key={post.id} className="profile-post-item">
                  <h4>{post.title}</h4>
                  <small>{formatDateTime(post.createdAtUtc)}</small>
                  {post.trailName && <p>Пътека: {post.trailName}</p>}
                  <p>{post.content}</p>
                  <div className="profile-actions-row">
                    <button type="button" className="secondary-btn" onClick={() => openEditPostModal(post)}>
                      Редактирай
                    </button>
                    <button
                      type="button"
                      className="secondary-btn"
                      onClick={() => void submitDeletePost(post.id)}
                      style={{ color: '#d32f2f' }}
                    >
                      Изтрий
                    </button>
                  </div>
                  <div className="post-image-grid">
                    {post.imageUrls.map((url) => (
                      <img key={url} src={url} alt={post.title} loading="lazy" />
                    ))}
                  </div>
                </article>
              ))
            )}
          </section>

          {/* Edit Post Modal */}
          {editingPostId && (
            <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
              <form
                style={{ backgroundColor: '#1a1a1a', padding: '20px', borderRadius: '8px', maxWidth: '600px', width: '90%' }}
                onSubmit={(event) => {
                  event.preventDefault();
                  void submitEditPost();
                }}
              >
                <h2>Редактирай публикация</h2>
                <label className="auth-label" htmlFor="edit-post-title">
                  Заглавие
                </label>
                <input
                  id="edit-post-title"
                  className="search-input auth-input"
                  type="text"
                  value={editPostTitle}
                  onChange={(event) => setEditPostTitle(event.target.value)}
                  placeholder="Заглавие"
                />

                <label className="auth-label" htmlFor="edit-post-content">
                  Съдържание
                </label>
                <textarea
                  id="edit-post-content"
                  className="search-input auth-input"
                  value={editPostContent}
                  onChange={(event) => setEditPostContent(event.target.value)}
                  placeholder="Съдържание"
                  rows={5}
                />

                <label className="auth-label" htmlFor="edit-post-trail-id">
                  Trail ID (по избор)
                </label>
                <input
                  id="edit-post-trail-id"
                  className="search-input auth-input"
                  type="number"
                  value={editPostTrailId}
                  onChange={(event) => setEditPostTrailId(event.target.value)}
                  placeholder="Trail ID"
                />

                {postEditError && <p className="status-text error">{postEditError}</p>}
                <div className="profile-actions-row">
                  <button type="submit" className="primary-btn" disabled={isUpdatingPost}>
                    {isUpdatingPost ? 'Обновлявам...' : 'Обнови публикация'}
                  </button>
                  <button type="button" className="secondary-btn" onClick={() => setEditingPostId(null)}>
                    Отмени
                  </button>
                </div>
              </form>
            </div>
          )}

          {/* Edit Profile Modal */}
          {showEditProfileModal && (
            <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
              <form
                style={{ backgroundColor: '#1a1a1a', padding: '20px', borderRadius: '8px', maxWidth: '600px', width: '90%' }}
                onSubmit={(event) => {
                  event.preventDefault();
                  void submitEditProfile();
                }}
              >
                <h2>Редактирай профил</h2>
                <label className="auth-label" htmlFor="edit-profile-email">
                  Імейл
                </label>
                <input
                  id="edit-profile-email"
                  className="search-input auth-input"
                  type="email"
                  value={editProfileEmail}
                  onChange={(event) => setEditProfileEmail(event.target.value)}
                  placeholder="you@example.com"
                />

                <label className="auth-label" htmlFor="edit-profile-username">
                  Потребителско име
                </label>
                <input
                  id="edit-profile-username"
                  className="search-input auth-input"
                  type="text"
                  value={editProfileUserName}
                  onChange={(event) => setEditProfileUserName(event.target.value)}
                  placeholder="Потребителско име"
                />

                <label className="auth-label" htmlFor="edit-profile-phone">
                  Телефонен номер (по избор)
                </label>
                <input
                  id="edit-profile-phone"
                  className="search-input auth-input"
                  type="tel"
                  value={editProfilePhoneNumber}
                  onChange={(event) => setEditProfilePhoneNumber(event.target.value)}
                  placeholder="+359 XXX XXX XXX"
                />

                {profileUpdateError && <p className="status-text error">{profileUpdateError}</p>}
                <div className="profile-actions-row">
                  <button type="submit" className="primary-btn" disabled={isUpdatingProfile}>
                    {isUpdatingProfile ? 'Обновлявам...' : 'Запази промени'}
                  </button>
                  <button type="button" className="secondary-btn" onClick={() => setShowEditProfileModal(false)}>
                    Отмени
                  </button>
                </div>
              </form>
            </div>
          )}

          {/* Change Password Modal */}
          {showChangePasswordModal && (
            <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
              <form
                style={{ backgroundColor: '#1a1a1a', padding: '20px', borderRadius: '8px', maxWidth: '600px', width: '90%' }}
                onSubmit={(event) => {
                  event.preventDefault();
                  void submitChangePassword();
                }}
              >
                <h2>Смени парола</h2>
                <label className="auth-label" htmlFor="current-password">
                  Текуща парола
                </label>
                <input
                  id="current-password"
                  className="search-input auth-input"
                  type="password"
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
                  placeholder="Текуща парола"
                />

                <label className="auth-label" htmlFor="new-password">
                  Нова парола
                </label>
                <input
                  id="new-password"
                  className="search-input auth-input"
                  type="password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  placeholder="Нова парола (мин. 6 символа, 1 цифра)"
                />

                <label className="auth-label" htmlFor="new-password-confirm">
                  Потвърди нова парола
                </label>
                <input
                  id="new-password-confirm"
                  className="search-input auth-input"
                  type="password"
                  value={newPasswordConfirm}
                  onChange={(event) => setNewPasswordConfirm(event.target.value)}
                  placeholder="Потвърди нова парола"
                />

                {passwordChangeError && <p className="status-text error">{passwordChangeError}</p>}
                {passwordChangeNotice && <p className="status-text">{passwordChangeNotice}</p>}
                <div className="profile-actions-row">
                  <button type="submit" className="primary-btn" disabled={isChangingPassword}>
                    {isChangingPassword ? 'Променям...' : 'Смени парола'}
                  </button>
                  <button type="button" className="secondary-btn" onClick={() => setShowChangePasswordModal(false)}>
                    Отмени
                  </button>
                </div>
              </form>
            </div>
          )}

          <section className="profile-card profile-security-card">
            <h3>
              <Shield size={16} />
              Сигурност
            </h3>
            <p className="status-text">
              Използвай бутоните по-горе в Профилния преглед за редакция на профилни данни и смяна на парола.
            </p>
            <p className="status-text">Логирай се отново в други браузъри/устройства, за да завършиш сесиите на други места.</p>
          </section>

          {isAdmin && (
            <section className="profile-card profile-admin-quality">
              <h3>Admin: Data Quality</h3>
              {qualityError && <p className="status-text error">{qualityError}</p>}
              {qualityNotice && <p className="status-text">{qualityNotice}</p>}
              {quality ? (
                <ul>
                  <li>Общо пътеки: {quality.totalTrails}</li>
                  <li>Липсващи координати: {quality.missingCoordinates}</li>
                  <li>Липсващи length hints: {quality.missingLengthHints}</li>
                  <li>Липсваща денивелация: {quality.missingElevationGain}</li>
                  <li>Липсващи описания: {quality.missingDescription}</li>
                  <li>Stale source previews: {quality.staleSourcePreviews}</li>
                </ul>
              ) : (
                <p className="status-text">Няма заредени метрики.</p>
              )}
              <div className="auth-admin-actions">
                <button
                  type="button"
                  className="secondary-btn"
                  onClick={() => void loadDataQuality()}
                  disabled={isRefreshingQuality}
                >
                  {isRefreshingQuality ? 'Опресняване...' : 'Опресни quality'}
                </button>
                <button
                  type="button"
                  className="primary-btn"
                  onClick={() => void runManualReEnrich()}
                  disabled={isManualReEnriching}
                >
                  {isManualReEnriching ? 'Re-enrich...' : 'Ръчно re-enrich'}
                </button>
              </div>

              <h4 style={{ marginTop: '16px' }}>Предложения за нови пътеки</h4>
              {pendingTrailProposalsError && <p className="status-text error">{pendingTrailProposalsError}</p>}
              {isLoadingPendingTrailProposals ? (
                <p className="status-text">Зареждане на предложенията...</p>
              ) : pendingTrailProposals.length === 0 ? (
                <p className="status-text">Няма чакащи предложения.</p>
              ) : (
                pendingTrailProposals.map((proposal) => (
                  <article key={proposal.id} className="profile-post-item" style={{ marginTop: '12px' }}>
                    <h4>{proposal.title}</h4>
                    <small>{formatDateTime(proposal.createdAtUtc)}</small>
                    <p>{proposal.content}</p>
                    {proposal.aiReview && (
                      <>
                        <p>
                          AI надеждност: <strong>{proposal.aiReview.reliabilityScore}%</strong>
                        </p>
                        <p>{proposal.aiReview.summary}</p>
                        {proposal.aiReview.warnings.length > 0 && (
                          <ul>
                            {proposal.aiReview.warnings.map((warning) => (
                              <li key={warning}>{warning}</li>
                            ))}
                          </ul>
                        )}
                      </>
                    )}
                    <button
                      type="button"
                      className="primary-btn"
                      onClick={() => void submitApproveProposal(proposal.id)}
                      disabled={approvingPostId === proposal.id}
                    >
                      {approvingPostId === proposal.id ? 'Одобряване...' : 'Одобри и създай Trail'}
                    </button>
                  </article>
                ))
              )}
            </section>
          )}

          <Link className="trail-link" to="/">
            Назад към началото
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="app-container auth-page">
      <div className="auth-card">
        <h1 className="app-title">{mode === 'login' ? 'Вход' : 'Регистрация'}</h1>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          {sessionExpired && <p className="status-text error">Сесията е изтекла. Влез отново.</p>}

          <label className="auth-label" htmlFor="auth-email">
            <User size={16} />
            Имейл
          </label>
          <input
            id="auth-email"
            className="search-input auth-input"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
          />

          <label className="auth-label" htmlFor="auth-password">
            <Lock size={16} />
            Парола
          </label>
          <input
            id="auth-password"
            className="search-input auth-input"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="Минимум 6 символа"
          />

          {error && <p className="status-text error">{error}</p>}

          <button type="submit" className="primary-btn auth-submit" disabled={isSubmitting}>
            {isSubmitting ? 'Изпращане...' : mode === 'login' ? 'Вход' : 'Регистрация'}
          </button>
        </form>

        <button
          type="button"
          className="secondary-btn"
          onClick={() => setMode((current) => (current === 'login' ? 'register' : 'login'))}
        >
          {mode === 'login' ? 'Нямаш акаунт? Регистрация' : 'Имаш акаунт? Вход'}
        </button>

        <Link className="trail-link" to="/">
          Назад към началото
        </Link>
      </div>
    </div>
  );
}

export default AuthPage;
