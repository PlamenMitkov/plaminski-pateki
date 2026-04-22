import { useEffect, useMemo, useState } from 'react';
import { Activity, Download, Lock, LogOut, MessageCircle, PlusCircle, Shield, Trash2, User } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import axios from 'axios';
import {
  changePassword,
  deleteAccount,
  type ChangePasswordRequest,
  type DeleteAccountRequest,
} from '../services/authService';
import { useAuthCapabilities } from '../hooks/useAuthCapabilities';
import { useFavorites } from '../hooks/useFavorites';
import {
  createCommunityPost,
  deleteCommunityPost,
  getMyCommunityPosts,
  updateCommunityPost,
  type CommunityPostResponse,
} from '../services/communityService';
import {
  createAssistantSession,
  deleteAssistantSession,
  getMyAssistantSessions,
  type AssistantSessionResponse,
} from '../services/assistantService';
import '../App.css';

const ASSISTANT_SESSION_STORAGE_KEY = 'ecotrails:assistantSessionId';

type ActivityItem = {
  id: string;
  title: string;
  subtitle: string;
  timestamp: string;
  type: 'session' | 'proposal' | 'post';
};

function getProposalStatusMeta(status: string): { label: string; chipClass: string } {
  switch (status) {
    case 'Pending':
      return { label: 'В изчакване', chipClass: 'assistant-chip-warning' };
    case 'Approved':
      return { label: 'Одобрено', chipClass: 'assistant-chip-positive' };
    case 'Rejected':
      return { label: 'Отхвърлено', chipClass: 'assistant-chip-danger' };
    default:
      return { label: 'Ново', chipClass: 'assistant-chip-neutral' };
  }
}

function formatDateTime(dateIso: string): string {
  const date = new Date(dateIso);
  if (Number.isNaN(date.getTime())) {
    return 'няма данни';
  }

  return new Intl.DateTimeFormat('bg-BG', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date);
}

function getRequestErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const responseData = error.response?.data;
    if (Array.isArray(responseData) && responseData.length > 0) {
      return responseData.join(' ');
    }

    if (typeof responseData === 'string' && responseData.trim().length > 0) {
      return responseData;
    }
  }

  return fallback;
}

function UserPage() {
  const navigate = useNavigate();
  const { authUser, sessionInfo, refreshSession, clearAuth } = useAuthCapabilities();
  const { favoriteIds, clearFavorites } = useFavorites();

  const [assistantSessions, setAssistantSessions] = useState<AssistantSessionResponse[]>([]);
  const [isLoadingAssistantSessions, setIsLoadingAssistantSessions] = useState(false);
  const [assistantSessionError, setAssistantSessionError] = useState('');

  const [myPosts, setMyPosts] = useState<CommunityPostResponse[]>([]);
  const [isLoadingPosts, setIsLoadingPosts] = useState(false);
  const [postError, setPostError] = useState('');
  const [postNotice, setPostNotice] = useState('');

  const [postTitle, setPostTitle] = useState('');
  const [postContent, setPostContent] = useState('');
  const [postTrailId, setPostTrailId] = useState('');
  const [postImages, setPostImages] = useState<File[]>([]);
  const [postIsTrailProposal, setPostIsTrailProposal] = useState(false);
  const [isPosting, setIsPosting] = useState(false);
  const [editingPostId, setEditingPostId] = useState<number | null>(null);
  const [editPostTitle, setEditPostTitle] = useState('');
  const [editPostContent, setEditPostContent] = useState('');
  const [editPostTrailId, setEditPostTrailId] = useState('');
  const [isUpdatingPost, setIsUpdatingPost] = useState(false);
  const [postEditError, setPostEditError] = useState('');

  const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [newPasswordConfirm, setNewPasswordConfirm] = useState('');
  const [isChangingPassword, setIsChangingPassword] = useState(false);
  const [passwordChangeError, setPasswordChangeError] = useState('');
  const [passwordChangeNotice, setPasswordChangeNotice] = useState('');

  const [showDeleteAccountModal, setShowDeleteAccountModal] = useState(false);
  const [deletePassword, setDeletePassword] = useState('');
  const [deleteConfirmationText, setDeleteConfirmationText] = useState('');
  const [isDeletingAccount, setIsDeletingAccount] = useState(false);
  const [deleteAccountError, setDeleteAccountError] = useState('');
  const [pendingDeleteSessionId, setPendingDeleteSessionId] = useState<string | null>(null);

  useEffect(() => {
    void refreshSession();
  }, [refreshSession]);

  useEffect(() => {
    if (!authUser) {
      setAssistantSessions([]);
      setMyPosts([]);
      return;
    }

    const loadAssistantSessions = async () => {
      try {
        setIsLoadingAssistantSessions(true);
        setAssistantSessionError('');
        const sessions = await getMyAssistantSessions(12);
        setAssistantSessions(sessions);
      } catch (error) {
        console.error('Грешка при зареждане на AI сесиите:', error);
        setAssistantSessionError('Неуспешно зареждане на AI сесиите.');
      } finally {
        setIsLoadingAssistantSessions(false);
      }
    };

    const loadMyPosts = async () => {
      try {
        setIsLoadingPosts(true);
        setPostError('');
        const posts = await getMyCommunityPosts();
        setMyPosts(posts);
      } catch (error) {
        console.error('Грешка при зареждане на публикациите:', error);
        setPostError('Неуспешно зареждане на публикациите.');
      } finally {
        setIsLoadingPosts(false);
      }
    };

    void Promise.all([loadAssistantSessions(), loadMyPosts()]);
  }, [authUser]);

  const lastAssistantSession = assistantSessions[0] ?? null;

  const activityItems = useMemo<ActivityItem[]>(() => {
    const items: ActivityItem[] = [
      ...assistantSessions.map((session) => ({
        id: `session-${session.sessionId}`,
        title: session.title || 'Нова AI сесия',
        subtitle: `${session.messageCount} съобщения`,
        timestamp: session.lastActivityAt,
        type: 'session' as const,
      })),
      ...myPosts.map((post) => ({
        id: `post-${post.id}`,
        title: post.title,
        subtitle: post.postType === 'TrailProposal'
          ? 'Предложение за нова екопътека'
          : post.trailName
            ? `Информация за ${post.trailName}`
            : 'Потребителска публикация',
        timestamp: post.createdAtUtc,
        type: post.postType === 'TrailProposal' ? 'proposal' as const : 'post' as const,
      })),
    ];

    return items
      .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())
      .slice(0, 8);
  }, [assistantSessions, myPosts]);

  const activityStats = useMemo(() => {
    const proposalCount = myPosts.filter((post) => post.postType === 'TrailProposal').length;
    const postCount = myPosts.length;
    const imageCount = myPosts.filter((post) => post.imageUrls.length > 0).length;
    const timestamps = activityItems.map((item) => new Date(item.timestamp).getTime()).filter(Number.isFinite);
    const lastActivity = timestamps.length > 0 ? new Date(Math.max(...timestamps)).toISOString() : null;

    return {
      proposalCount,
      postCount,
      imageCount,
      sessionCount: assistantSessions.length,
      favoriteCount: favoriteIds.length,
      lastActivity,
    };
  }, [activityItems, assistantSessions.length, favoriteIds.length, myPosts]);

  const submitPost = async () => {
    if (!authUser) {
      setPostError('Трябва да си влязъл, за да публикуваш.');
      return;
    }

    try {
      setIsPosting(true);
      setPostError('');
      setPostNotice('');

      const trailIdNumeric = Number(postTrailId);
      const created = await createCommunityPost({
        title: postTitle,
        content: postContent,
        trailId: Number.isInteger(trailIdNumeric) && trailIdNumeric > 0 ? trailIdNumeric : undefined,
        postType: postIsTrailProposal ? 'TrailProposal' : undefined,
        images: postImages,
      });

      setMyPosts((current) => [created, ...current]);
      setPostTitle('');
      setPostContent('');
      setPostTrailId('');
      setPostImages([]);
      setPostIsTrailProposal(false);
      setPostNotice(
        created.postType === 'TrailProposal'
          ? 'Предложението е изпратено за преглед.'
          : 'Информацията за пътеката е публикувана успешно.',
      );
    } catch (error) {
      console.error('Грешка при създаване на публикация:', error);
      setPostError(getRequestErrorMessage(error, 'Неуспешно създаване на публикация.'));
    } finally {
      setIsPosting(false);
    }
  };

  const submitDeletePost = async (postId: number) => {
    if (!confirm('Сигурен ли си, че искаш да изтриеш тази публикация?')) {
      return;
    }

    try {
      await deleteCommunityPost(postId);
      setMyPosts((current) => current.filter((post) => post.id !== postId));
      setPostNotice('Публикацията е изтрита успешно.');
    } catch (error) {
      console.error('Грешка при изтриване на публикация:', error);
      setPostError(getRequestErrorMessage(error, 'Неуспешно изтриване на публикация.'));
    }
  };

  const openEditPostModal = (post: CommunityPostResponse) => {
    setEditingPostId(post.id);
    setEditPostTitle(post.title);
    setEditPostContent(post.content);
    setEditPostTrailId(post.trailId ? String(post.trailId) : '');
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
      const updated = await updateCommunityPost(editingPostId, {
        title: editPostTitle,
        content: editPostContent,
        trailId: Number.isInteger(trailIdNumeric) && trailIdNumeric > 0 ? trailIdNumeric : undefined,
      });

      setMyPosts((current) => current.map((post) => (post.id === updated.id ? updated : post)));
      setEditingPostId(null);
      setEditPostTitle('');
      setEditPostContent('');
      setEditPostTrailId('');
      setPostNotice('Публикацията е обновена успешно.');
    } catch (error) {
      console.error('Грешка при редакция на публикация:', error);
      setPostEditError(getRequestErrorMessage(error, 'Неуспешно редактиране на публикация.'));
    } finally {
      setIsUpdatingPost(false);
    }
  };

  const submitChangePassword = async () => {
    if (!currentPassword.trim()) {
      setPasswordChangeError('Въведи текущата си парола.');
      return;
    }

    if (!newPassword.trim()) {
      setPasswordChangeError('Въведи нова парола.');
      return;
    }

    if (!/\d/.test(newPassword)) {
      setPasswordChangeError('Новата парола трябва да съдържа поне една цифра.');
      return;
    }

    if (newPassword !== newPasswordConfirm) {
      setPasswordChangeError('Потвърждението на новата парола не съвпада.');
      return;
    }

    try {
      setIsChangingPassword(true);
      setPasswordChangeError('');
      setPasswordChangeNotice('');

      const request: ChangePasswordRequest = {
        currentPassword,
        newPassword,
      };

      await changePassword(request);
      setShowChangePasswordModal(false);
      setCurrentPassword('');
      setNewPassword('');
      setNewPasswordConfirm('');
      setPasswordChangeNotice('Паролата е сменена успешно.');
      await refreshSession();
    } catch (error) {
      console.error('Грешка при смяна на парола:', error);
      setPasswordChangeError(getRequestErrorMessage(error, 'Неуспешна смяна на паролата.'));
    } finally {
      setIsChangingPassword(false);
    }
  };

  const submitDeleteAccount = async () => {
    if (!deletePassword.trim()) {
      setDeleteAccountError('Въведи текущата си парола.');
      return;
    }

    if (deleteConfirmationText.trim().toUpperCase() !== 'DELETE') {
      setDeleteAccountError('За потвърждение въведи DELETE.');
      return;
    }

    try {
      setIsDeletingAccount(true);
      setDeleteAccountError('');

      const request: DeleteAccountRequest = {
        currentPassword: deletePassword,
        confirmationText: deleteConfirmationText,
      };

      await deleteAccount(request);
      localStorage.removeItem(ASSISTANT_SESSION_STORAGE_KEY);
      clearFavorites();
      clearAuth();
      navigate('/', { replace: true });
    } catch (error) {
      console.error('Грешка при изтриване на акаунт:', error);
      setDeleteAccountError(getRequestErrorMessage(error, 'Неуспешно изтриване на акаунта.'));
    } finally {
      setIsDeletingAccount(false);
    }
  };

  const openAssistantSession = (sessionId: string) => {
    localStorage.setItem(ASSISTANT_SESSION_STORAGE_KEY, sessionId);
    navigate('/?tab=assistant');
  };

  const removeAssistantSession = async (sessionId: string) => {
    try {
      setAssistantSessionError('');
      await deleteAssistantSession(sessionId);
      if (localStorage.getItem(ASSISTANT_SESSION_STORAGE_KEY) === sessionId) {
        localStorage.removeItem(ASSISTANT_SESSION_STORAGE_KEY);
      }

      setAssistantSessions((current) => current.filter((session) => session.sessionId !== sessionId));
      setPendingDeleteSessionId(null);
    } catch (error) {
      console.error('Грешка при изтриване на AI сесия:', error);
      setAssistantSessionError(getRequestErrorMessage(error, 'Неуспешно изтриване на AI сесията.'));
    }
  };

  const startNewAssistantSession = async () => {
    try {
      setAssistantSessionError('');
      const session = await createAssistantSession('Нова чат сесия');
      localStorage.setItem(ASSISTANT_SESSION_STORAGE_KEY, session.sessionId);
      navigate('/?tab=assistant');
    } catch (error) {
      console.error('Грешка при създаване на AI сесия:', error);
      setAssistantSessionError(getRequestErrorMessage(error, 'Неуспешно създаване на AI сесия.'));
    }
  };

  const exportActivitySnapshot = async () => {
    const payload = {
      exportedAtUtc: new Date().toISOString(),
      account: {
        email: authUser?.email ?? '',
        roles: sessionInfo?.roles ?? [],
      },
      stats: activityStats,
      assistantSessions,
      posts: myPosts,
      favoriteTrailIds: favoriteIds,
    };

    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: 'application/json;charset=utf-8',
    });

    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `ecotrails-user-activity-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(link.href);
  };

  if (!authUser) {
    return (
      <div className="app-container auth-page profile-hub-page user-page">
        <div className="auth-card profile-hub-card">
          <h1 className="app-title">
            <User size={20} />
            Потребителски профил
          </h1>
          <p className="status-text">Влез в профила си, за да управляваш активност, предложения и сигурност.</p>
          <div className="profile-actions-row">
            <Link className="primary-btn auth-link-btn" to="/auth">
              Вход / Регистрация
            </Link>
            <Link className="secondary-btn" to="/">
              Към началото
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="app-container auth-page profile-hub-page user-page">
      <div className="auth-card profile-hub-card">
        <div className="profile-hub-header">
          <h1 className="app-title">
            <User size={20} />
            Потребителски профил
          </h1>
          <button type="button" className="secondary-btn" onClick={clearAuth}>
            <LogOut size={16} />
            Изход
          </button>
        </div>

        <div className="profile-grid">
          <section className="profile-card">
            <h3>
              <Shield size={16} />
              Акаунт и сигурност
            </h3>
            <p className="status-text">Имейл: {authUser.email}</p>
            {sessionInfo?.roles?.length ? (
              <div className="profile-role-list">
                {sessionInfo.roles.map((role) => (
                  <span key={role} className="assistant-chip assistant-chip-positive">
                    {role}
                  </span>
                ))}
              </div>
            ) : null}
            {passwordChangeNotice && <p className="status-text">{passwordChangeNotice}</p>}
            <div className="profile-actions-row">
              <button type="button" className="primary-btn" onClick={() => setShowChangePasswordModal(true)}>
                <Lock size={16} />
                Смени парола
              </button>
              <button type="button" className="secondary-btn" onClick={() => setShowDeleteAccountModal(true)}>
                <Trash2 size={16} />
                Изтрий акаунт
              </button>
            </div>
            <div className="profile-actions-row">
              <button type="button" className="secondary-btn" onClick={() => void exportActivitySnapshot()}>
                <Download size={16} />
                Експорт на активност
              </button>
              <button type="button" className="secondary-btn" onClick={() => navigate('/?tab=favorites')}>
                Любими пътеки
              </button>
            </div>
            <p className="status-text">Екстра: можеш да експортираш своята активност и предложения в JSON архив.</p>
          </section>

          <section className="profile-card">
            <h3>
              <Activity size={16} />
              Активност
            </h3>
            <div className="profile-stats-grid">
              <div className="chart-card">
                <h4>AI сесии</h4>
                <p>{activityStats.sessionCount}</p>
              </div>
              <div className="chart-card">
                <h4>Публикации</h4>
                <p>{activityStats.postCount}</p>
              </div>
              <div className="chart-card">
                <h4>Предложения</h4>
                <p>{activityStats.proposalCount}</p>
              </div>
              <div className="chart-card">
                <h4>Любими</h4>
                <p>{activityStats.favoriteCount}</p>
              </div>
            </div>
            <p className="status-text">
              Последна активност: {activityStats.lastActivity ? formatDateTime(activityStats.lastActivity) : 'Няма активност засега'}
            </p>
            <div className="user-activity-list">
              {activityItems.length === 0 ? (
                <p className="status-text">Все още няма записана активност.</p>
              ) : (
                activityItems.map((item) => (
                  <article key={item.id} className={`user-activity-item user-activity-${item.type}`}>
                    <strong>{item.title}</strong>
                    <span>{item.subtitle}</span>
                    <small>{formatDateTime(item.timestamp)}</small>
                  </article>
                ))
              )}
            </div>
          </section>

          <section className="profile-card profile-assistant-card">
            <h3>
              <MessageCircle size={16} />
              Моите AI сесии
            </h3>
            {assistantSessionError && <p className="status-text error">{assistantSessionError}</p>}
            <div className="profile-actions-row">
              <button type="button" className="primary-btn" onClick={() => void startNewAssistantSession()}>
                <PlusCircle size={16} />
                Нова AI сесия
              </button>
              {lastAssistantSession && (
                <button
                  type="button"
                  className="secondary-btn"
                  onClick={() => openAssistantSession(lastAssistantSession.sessionId)}
                >
                  Продължи последната
                </button>
              )}
            </div>
            {isLoadingAssistantSessions ? (
              <p className="status-text">Зареждане на сесиите...</p>
            ) : assistantSessions.length === 0 ? (
              <p className="status-text">Все още нямаш запазени AI сесии.</p>
            ) : (
              <div className="assistant-session-list">
                {assistantSessions.map((session) => (
                  <div key={session.sessionId} className="assistant-session-item">
                    <button
                      type="button"
                      className="assistant-session-open"
                      onClick={() => openAssistantSession(session.sessionId)}
                    >
                      <span>{session.title || 'Нова AI сесия'}</span>
                      <small>{session.messageCount} съобщения • {formatDateTime(session.lastActivityAt)}</small>
                    </button>
                    <button
                      type="button"
                      className="assistant-session-delete"
                      onClick={() => setPendingDeleteSessionId(session.sessionId)}
                      title="Изтрий AI сесия"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="profile-card profile-post-form">
            <h3>Ново предложение или актуална информация</h3>
            <p className="status-text">
              Добави нова екопътека, която вече си проверил на място, или качи актуална информация за съществуващ маршрут.
            </p>
            <form
              onSubmit={(event) => {
                event.preventDefault();
                void submitPost();
              }}
            >
              <label className="auth-label" htmlFor="user-post-title">
                Заглавие
              </label>
              <input
                id="user-post-title"
                className="search-input auth-input"
                type="text"
                value={postTitle}
                onChange={(event) => setPostTitle(event.target.value)}
                placeholder="Напр. Проверих маркировката край Бистрица"
              />

              <label className="auth-label" htmlFor="user-post-content">
                Описание
              </label>
              <textarea
                id="user-post-content"
                className="search-input auth-input"
                value={postContent}
                onChange={(event) => setPostContent(event.target.value)}
                placeholder="Опиши какво си видял на място: достъп, маркировка, вода, опасности, сезонни условия..."
                rows={6}
              />

              <label className="auth-label" htmlFor="user-post-trail-id">
                Trail ID (ако информацията е за съществуваща пътека)
              </label>
              <input
                id="user-post-trail-id"
                className="search-input auth-input"
                type="number"
                value={postTrailId}
                onChange={(event) => setPostTrailId(event.target.value)}
                placeholder="Пример: 42"
              />

              <label className="auth-label" htmlFor="user-post-images">
                Снимки (до 4)
              </label>
              <input
                id="user-post-images"
                className="search-input auth-input"
                type="file"
                multiple
                accept="image/jpeg,image/png,image/webp"
                onChange={(event) => setPostImages(Array.from(event.target.files ?? []))}
              />

              <label className="auth-label" htmlFor="user-post-is-proposal">
                <input
                  id="user-post-is-proposal"
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
                  {isPosting ? 'Публикуване...' : 'Изпрати'}
                </button>
              </div>
            </form>
          </section>

          <section className="profile-card profile-post-list">
            <h3>Моите последни публикации</h3>
            {isLoadingPosts ? (
              <p className="status-text">Зареждане на публикациите...</p>
            ) : myPosts.length === 0 ? (
              <p className="status-text">Все още нямаш публикации.</p>
            ) : (
              myPosts.slice(0, 8).map((post) => (
                <article key={post.id} className="profile-post-item">
                  <div className="user-post-header">
                    <h4>{post.title}</h4>
                    <span className={`assistant-chip ${getProposalStatusMeta(post.proposalStatus).chipClass}`}>
                      {getProposalStatusMeta(post.proposalStatus).label}
                    </span>
                  </div>
                  <small>{formatDateTime(post.createdAtUtc)}</small>
                  <p>
                    {post.postType === 'TrailProposal'
                      ? 'Предложение за нова екопътека'
                      : post.trailName
                        ? `Свързано с: ${post.trailName}`
                        : 'Публикация без конкретна пътека'}
                  </p>
                  {post.proposalStatus === 'Rejected' && post.rejectionReason && (
                    <p className="user-post-note user-post-note-rejected">Причина за отказ: {post.rejectionReason}</p>
                  )}
                  {post.proposalStatus === 'Approved' && post.trailName && (
                    <p className="user-post-note user-post-note-approved">Одобрено и свързано с: {post.trailName}</p>
                  )}
                  <p>{post.content}</p>
                  {post.imageUrls.length > 0 && (
                    <div className="post-image-grid">
                      {post.imageUrls.map((url) => (
                        <img key={url} src={url} alt={post.title} loading="lazy" />
                      ))}
                    </div>
                  )}
                  <div className="profile-actions-row">
                    <button
                      type="button"
                      className="secondary-btn"
                      onClick={() => openEditPostModal(post)}
                    >
                      Редактирай
                    </button>
                    <button
                      type="button"
                      className="secondary-btn"
                      onClick={() => void submitDeletePost(post.id)}
                    >
                      Изтрий
                    </button>
                  </div>
                </article>
              ))
            )}
          </section>
        </div>

        {showChangePasswordModal && (
          <div className="assistant-modal-backdrop" role="dialog" aria-modal="true">
            <form
              className="assistant-modal-card user-modal-card"
              onSubmit={(event) => {
                event.preventDefault();
                void submitChangePassword();
              }}
            >
              <h3>Смени парола</h3>
              <label className="auth-label" htmlFor="current-password">
                Текуща парола
              </label>
              <input
                id="current-password"
                className="search-input auth-input"
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
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
              />

              {passwordChangeError && <p className="status-text error">{passwordChangeError}</p>}
              <div className="assistant-modal-actions">
                <button type="button" className="secondary-btn" onClick={() => setShowChangePasswordModal(false)}>
                  Отказ
                </button>
                <button type="submit" className="primary-btn" disabled={isChangingPassword}>
                  {isChangingPassword ? 'Променям...' : 'Смени парола'}
                </button>
              </div>
            </form>
          </div>
        )}

        {editingPostId && (
          <div className="assistant-modal-backdrop" role="dialog" aria-modal="true">
            <form
              className="assistant-modal-card user-modal-card"
              onSubmit={(event) => {
                event.preventDefault();
                void submitEditPost();
              }}
            >
              <h3>Редактирай публикация</h3>
              <label className="auth-label" htmlFor="edit-post-title">
                Заглавие
              </label>
              <input
                id="edit-post-title"
                className="search-input auth-input"
                type="text"
                value={editPostTitle}
                onChange={(event) => setEditPostTitle(event.target.value)}
              />

              <label className="auth-label" htmlFor="edit-post-content">
                Описание
              </label>
              <textarea
                id="edit-post-content"
                className="search-input auth-input"
                value={editPostContent}
                onChange={(event) => setEditPostContent(event.target.value)}
                rows={6}
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
              />

              {postEditError && <p className="status-text error">{postEditError}</p>}
              <div className="assistant-modal-actions">
                <button type="button" className="secondary-btn" onClick={() => setEditingPostId(null)}>
                  Отказ
                </button>
                <button type="submit" className="primary-btn" disabled={isUpdatingPost}>
                  {isUpdatingPost ? 'Запазване...' : 'Запази'}
                </button>
              </div>
            </form>
          </div>
        )}

        {pendingDeleteSessionId && (
          <div className="assistant-modal-backdrop" role="dialog" aria-modal="true">
            <div className="assistant-modal-card user-modal-card" onClick={(event) => event.stopPropagation()}>
              <h3>Изтриване на AI сесия</h3>
              <p>Сигурен ли си, че искаш да изтриеш тази AI сесия?</p>
              <div className="assistant-modal-actions">
                <button type="button" className="secondary-btn" onClick={() => setPendingDeleteSessionId(null)}>
                  Отказ
                </button>
                <button
                  type="button"
                  className="primary-btn"
                  onClick={() => void removeAssistantSession(pendingDeleteSessionId)}
                >
                  Изтрий
                </button>
              </div>
            </div>
          </div>
        )}

        {showDeleteAccountModal && (
          <div className="assistant-modal-backdrop" role="dialog" aria-modal="true">
            <form
              className="assistant-modal-card user-modal-card user-danger-card"
              onSubmit={(event) => {
                event.preventDefault();
                void submitDeleteAccount();
              }}
            >
              <h3>Изтриване на акаунт</h3>
              <p>Това действие е необратимо. За потвърждение въведи паролата си и текста DELETE.</p>

              <label className="auth-label" htmlFor="delete-account-password">
                Текуща парола
              </label>
              <input
                id="delete-account-password"
                className="search-input auth-input"
                type="password"
                value={deletePassword}
                onChange={(event) => setDeletePassword(event.target.value)}
              />

              <label className="auth-label" htmlFor="delete-account-confirmation">
                Потвърждение
              </label>
              <input
                id="delete-account-confirmation"
                className="search-input auth-input"
                type="text"
                value={deleteConfirmationText}
                onChange={(event) => setDeleteConfirmationText(event.target.value)}
                placeholder="Въведи DELETE"
              />

              {deleteAccountError && <p className="status-text error">{deleteAccountError}</p>}
              <div className="assistant-modal-actions">
                <button type="button" className="secondary-btn" onClick={() => setShowDeleteAccountModal(false)}>
                  Отказ
                </button>
                <button type="submit" className="primary-btn" disabled={isDeletingAccount}>
                  {isDeletingAccount ? 'Изтриване...' : 'Изтрий акаунта'}
                </button>
              </div>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}

export default UserPage;
