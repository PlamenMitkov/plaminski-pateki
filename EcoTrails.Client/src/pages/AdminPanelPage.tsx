import { useCallback, useEffect, useRef, useState } from 'react';
import {
  adminPanelLogin,
  approveAdminProposal,
  clearPanelToken,
  getAdminProposals,
  getPanelToken,
  rejectAdminProposal,
  setPanelToken,
  type AdminPanelApproveRequest,
  type AdminPanelProposalResponse,
} from '../services/adminPanelService';

// ─── Types ────────────────────────────────────────────────────────────────────

type StatusFilter = 'Pending' | 'Approved' | 'Rejected' | 'All';

interface ApproveFormState {
  name: string;
  location: string;
  region: string;
  difficultyLevel: string;
  durationInHours: string;
  elevationGain: string;
  latitude: string;
  longitude: string;
  waterSources: boolean;
  suitableForKids: boolean;
  maxAltitude: string;
  requiredGearJson: string;
}

const DEFAULT_APPROVE_FORM: ApproveFormState = {
  name: '',
  location: '',
  region: '',
  difficultyLevel: 'Moderate',
  durationInHours: '',
  elevationGain: '',
  latitude: '',
  longitude: '',
  waterSources: false,
  suitableForKids: false,
  maxAltitude: '',
  requiredGearJson: '',
};

// ─── Score badge helper ────────────────────────────────────────────────────────

function ScoreBadge({ score }: { score: number }) {
  const color = score >= 70 ? '#22c55e' : score >= 40 ? '#f59e0b' : '#ef4444';
  return (
    <span
      style={{
        display: 'inline-block',
        padding: '2px 10px',
        borderRadius: '999px',
        background: color,
        color: '#fff',
        fontWeight: 700,
        fontSize: '0.82rem',
        minWidth: '2.2rem',
        textAlign: 'center',
      }}
    >
      {score}
    </span>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colorMap: Record<string, string> = {
    Pending: '#f59e0b',
    Approved: '#22c55e',
    Rejected: '#ef4444',
    None: '#6b7280',
  };
  const labelMap: Record<string, string> = {
    Pending: 'В изчакване',
    Approved: 'Одобрено',
    Rejected: 'Отхвърлено',
    None: 'Ново',
  };
  const bg = colorMap[status] ?? '#6b7280';
  return (
    <span
      style={{
        display: 'inline-block',
        padding: '2px 10px',
        borderRadius: '999px',
        background: bg,
        color: '#fff',
        fontWeight: 600,
        fontSize: '0.78rem',
      }}
    >
      {labelMap[status] ?? status}
    </span>
  );
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function AdminPanelPage() {
  const [isLoggedIn, setIsLoggedIn] = useState(() => !!getPanelToken());

  // Login form
  const [loginUsername, setLoginUsername] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [loginError, setLoginError] = useState('');
  const [isLoggingIn, setIsLoggingIn] = useState(false);

  // Proposals list
  const [proposals, setProposals] = useState<AdminPanelProposalResponse[]>([]);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('Pending');
  const [isLoadingProposals, setIsLoadingProposals] = useState(false);
  const [proposalsError, setProposalsError] = useState('');

  // Approve modal
  const [approveTarget, setApproveTarget] = useState<AdminPanelProposalResponse | null>(null);
  const [approveForm, setApproveForm] = useState<ApproveFormState>(DEFAULT_APPROVE_FORM);
  const [isApproving, setIsApproving] = useState(false);
  const [approveError, setApproveError] = useState('');

  // Reject modal
  const [rejectTarget, setRejectTarget] = useState<AdminPanelProposalResponse | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [isRejecting, setIsRejecting] = useState(false);
  const [rejectError, setRejectError] = useState('');

  const loginUsernameRef = useRef<HTMLInputElement>(null);

  // ── Login ────────────────────────────────────────────────────────────────

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError('');
    if (!loginUsername.trim() || !loginPassword) {
      setLoginError('Въведете потребителско име и парола.');
      return;
    }
    setIsLoggingIn(true);
    try {
      const token = await adminPanelLogin(loginUsername.trim(), loginPassword);
      setPanelToken(token);
      setIsLoggedIn(true);
    } catch {
      setLoginError('Невалидни потребителско име или парола.');
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleLogout = () => {
    clearPanelToken();
    setIsLoggedIn(false);
    setProposals([]);
  };

  // ── Load proposals ───────────────────────────────────────────────────────

  const loadProposals = useCallback(async () => {
    setIsLoadingProposals(true);
    setProposalsError('');
    try {
      const filter = statusFilter === 'All' ? undefined : statusFilter;
      const data = await getAdminProposals(filter);
      setProposals(data);
    } catch {
      setProposalsError('Грешка при зареждане на предложенията.');
      if (!getPanelToken()) {
        setIsLoggedIn(false);
      }
    } finally {
      setIsLoadingProposals(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    if (isLoggedIn) {
      void loadProposals();
    }
  }, [isLoggedIn, loadProposals]);

  // ── Approve modal ────────────────────────────────────────────────────────

  const openApproveModal = (proposal: AdminPanelProposalResponse) => {
    const ai = proposal.aiReview;
    setApproveForm({
      name: ai?.suggestedName || proposal.title,
      location: ai?.suggestedLocation || '',
      region: ai?.suggestedRegion || '',
      difficultyLevel: ai?.suggestedDifficultyLevel || 'Moderate',
      durationInHours: '',
      elevationGain: '',
      latitude: '',
      longitude: '',
      waterSources: false,
      suitableForKids: false,
      maxAltitude: '',
      requiredGearJson: '',
    });
    setApproveError('');
    setApproveTarget(proposal);
  };

  const handleApprove = async () => {
    if (!approveTarget) return;
    setIsApproving(true);
    setApproveError('');
    try {
      const req: AdminPanelApproveRequest = {
        name: approveForm.name.trim() || undefined,
        location: approveForm.location.trim() || undefined,
        region: approveForm.region.trim() || undefined,
        difficultyLevel: approveForm.difficultyLevel || undefined,
        durationInHours: approveForm.durationInHours ? parseFloat(approveForm.durationInHours) : undefined,
        elevationGain: approveForm.elevationGain ? parseInt(approveForm.elevationGain, 10) : undefined,
        latitude: approveForm.latitude ? parseFloat(approveForm.latitude) : undefined,
        longitude: approveForm.longitude ? parseFloat(approveForm.longitude) : undefined,
        waterSources: approveForm.waterSources,
        suitableForKids: approveForm.suitableForKids,
        maxAltitude: approveForm.maxAltitude ? parseInt(approveForm.maxAltitude, 10) : undefined,
        requiredGearJson: approveForm.requiredGearJson.trim() || undefined,
      };
      await approveAdminProposal(approveTarget.id, req);
      setApproveTarget(null);
      await loadProposals();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Грешка при одобряване.';
      setApproveError(msg);
    } finally {
      setIsApproving(false);
    }
  };

  // ── Reject modal ─────────────────────────────────────────────────────────

  const openRejectModal = (proposal: AdminPanelProposalResponse) => {
    setRejectReason('');
    setRejectError('');
    setRejectTarget(proposal);
  };

  const handleReject = async () => {
    if (!rejectTarget) return;
    if (rejectReason.trim().length < 4) {
      setRejectError('Причината трябва да е поне 4 символа.');
      return;
    }
    setIsRejecting(true);
    setRejectError('');
    try {
      await rejectAdminProposal(rejectTarget.id, rejectReason.trim());
      setRejectTarget(null);
      await loadProposals();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Грешка при отхвърляне.';
      setRejectError(msg);
    } finally {
      setIsRejecting(false);
    }
  };

  // ── Render: login ────────────────────────────────────────────────────────

  if (!isLoggedIn) {
    return (
      <div style={styles.loginWrap}>
        <div style={styles.loginCard}>
          <div style={styles.loginLogo}>
            <i className="fas fa-shield-halved" style={{ fontSize: '2rem', color: '#22c55e' }} />
          </div>
          <h1 style={styles.loginTitle}>Администраторски панел</h1>
          <p style={styles.loginSub}>EcoTrails — Управление на предложения</p>
          <form onSubmit={(e) => { void handleLogin(e); }} style={styles.loginForm}>
            <label style={styles.loginLabel}>Потребителско име</label>
            <input
              ref={loginUsernameRef}
              type="text"
              autoComplete="username"
              value={loginUsername}
              onChange={(e) => setLoginUsername(e.target.value)}
              style={styles.loginInput}
              placeholder="ecoadmin"
              disabled={isLoggingIn}
            />
            <label style={styles.loginLabel}>Парола</label>
            <input
              type="password"
              autoComplete="current-password"
              value={loginPassword}
              onChange={(e) => setLoginPassword(e.target.value)}
              style={styles.loginInput}
              placeholder="••••••••"
              disabled={isLoggingIn}
            />
            {loginError && <div style={styles.errorBanner}>{loginError}</div>}
            <button type="submit" style={styles.loginBtn} disabled={isLoggingIn}>
              {isLoggingIn ? 'Влизане…' : 'Влез'}
            </button>
          </form>
        </div>
      </div>
    );
  }

  // ── Render: main panel ───────────────────────────────────────────────────

  return (
    <div style={styles.panelWrap}>
      {/* Header */}
      <header style={styles.header}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <i className="fas fa-shield-halved" style={{ color: '#22c55e', fontSize: '1.3rem' }} />
          <span style={{ fontWeight: 700, fontSize: '1.1rem' }}>EcoTrails Админ панел</span>
        </div>
        <button onClick={handleLogout} style={styles.logoutBtn}>
          <i className="fas fa-right-from-bracket" /> Изход
        </button>
      </header>

      {/* Filters */}
      <div style={styles.filterBar}>
        <span style={{ color: '#94a3b8', fontSize: '0.85rem', marginRight: 8 }}>Филтър:</span>
        {(['Pending', 'All', 'Approved', 'Rejected'] as StatusFilter[]).map((f) => (
          <button
            key={f}
            onClick={() => setStatusFilter(f)}
            style={{
              ...styles.filterBtn,
              background: statusFilter === f ? '#22c55e' : '#1e293b',
              color: statusFilter === f ? '#fff' : '#94a3b8',
            }}
          >
            {f === 'Pending' ? 'В изчакване' : f === 'Approved' ? 'Одобрени' : f === 'Rejected' ? 'Отхвърлени' : 'Всички'}
          </button>
        ))}
        <button onClick={() => { void loadProposals(); }} style={styles.reloadBtn} disabled={isLoadingProposals}>
          <i className={`fas fa-rotate-right${isLoadingProposals ? ' fa-spin' : ''}`} />
        </button>
      </div>

      {/* Content */}
      <div style={styles.content}>
        {isLoadingProposals && (
          <div style={styles.loadingMsg}>
            <i className="fas fa-circle-notch fa-spin" /> Зареждане…
          </div>
        )}
        {proposalsError && <div style={styles.errorBanner}>{proposalsError}</div>}
        {!isLoadingProposals && proposals.length === 0 && !proposalsError && (
          <div style={styles.emptyMsg}>Няма предложения за показване.</div>
        )}
        <div style={styles.proposalGrid}>
          {proposals.map((p) => (
            <ProposalCard
              key={p.id}
              proposal={p}
              onApprove={() => openApproveModal(p)}
              onReject={() => openRejectModal(p)}
            />
          ))}
        </div>
      </div>

      {/* Approve modal */}
      {approveTarget && (
        <Modal onClose={() => setApproveTarget(null)}>
          <h2 style={styles.modalTitle}>
            <i className="fas fa-circle-check" style={{ color: '#22c55e' }} /> Одобри предложение
          </h2>
          <p style={styles.modalSub}>
            Прегледайте и коригирайте полетата преди да създадете пътеката.
          </p>
          <div style={styles.formGrid}>
            <InputField label="Име на пътека *" value={approveForm.name} onChange={(v) => setApproveForm((f) => ({ ...f, name: v }))} />
            <InputField label="Местоположение" value={approveForm.location} onChange={(v) => setApproveForm((f) => ({ ...f, location: v }))} />
            <InputField label="Регион" value={approveForm.region} onChange={(v) => setApproveForm((f) => ({ ...f, region: v }))} />
            <SelectField
              label="Трудност"
              value={approveForm.difficultyLevel}
              onChange={(v) => setApproveForm((f) => ({ ...f, difficultyLevel: v }))}
              options={['Easy', 'Moderate', 'Difficult']}
              labels={['Лесна', 'Умерена', 'Трудна']}
            />
            <InputField label="Продължителност (часове)" value={approveForm.durationInHours} type="number" onChange={(v) => setApproveForm((f) => ({ ...f, durationInHours: v }))} />
            <InputField label="Денивелация (м)" value={approveForm.elevationGain} type="number" onChange={(v) => setApproveForm((f) => ({ ...f, elevationGain: v }))} />
            <InputField label="Макс. altitude (м)" value={approveForm.maxAltitude} type="number" onChange={(v) => setApproveForm((f) => ({ ...f, maxAltitude: v }))} />
            <InputField label="Latitude" value={approveForm.latitude} type="number" onChange={(v) => setApproveForm((f) => ({ ...f, latitude: v }))} />
            <InputField label="Longitude" value={approveForm.longitude} type="number" onChange={(v) => setApproveForm((f) => ({ ...f, longitude: v }))} />
          </div>
          <div style={{ display: 'flex', gap: 16, marginTop: 8 }}>
            <CheckboxField
              label="Водни източници"
              checked={approveForm.waterSources}
              onChange={(v) => setApproveForm((f) => ({ ...f, waterSources: v }))}
            />
            <CheckboxField
              label="Подходяща за деца"
              checked={approveForm.suitableForKids}
              onChange={(v) => setApproveForm((f) => ({ ...f, suitableForKids: v }))}
            />
          </div>
          <div style={{ marginTop: 8 }}>
            <label style={styles.label}>Необходимо оборудване (JSON масив)</label>
            <textarea
              value={approveForm.requiredGearJson}
              onChange={(e) => setApproveForm((f) => ({ ...f, requiredGearJson: e.target.value }))}
              style={{ ...styles.input, height: 60, resize: 'vertical' }}
              placeholder='["Туристически обувки", "Дъждобран"]'
            />
          </div>
          {approveError && <div style={styles.errorBanner}>{approveError}</div>}
          <div style={styles.modalActions}>
            <button onClick={() => setApproveTarget(null)} style={styles.cancelBtn}>Отказ</button>
            <button onClick={() => { void handleApprove(); }} style={styles.confirmApproveBtn} disabled={isApproving}>
              {isApproving ? 'Одобряване…' : 'Одобри и създай Trail'}
            </button>
          </div>
        </Modal>
      )}

      {/* Reject modal */}
      {rejectTarget && (
        <Modal onClose={() => setRejectTarget(null)}>
          <h2 style={styles.modalTitle}>
            <i className="fas fa-circle-xmark" style={{ color: '#ef4444' }} /> Отхвърли предложение
          </h2>
          <p style={{ ...styles.modalSub, marginBottom: 12 }}>
            <strong>{rejectTarget.title}</strong>
          </p>
          <label style={styles.label}>Причина за отхвърляне *</label>
          <textarea
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            style={{ ...styles.input, height: 90, resize: 'vertical' }}
            placeholder="Обяснете защо предложението не отговаря на изискванията…"
            autoFocus
          />
          {rejectError && <div style={styles.errorBanner}>{rejectError}</div>}
          <div style={styles.modalActions}>
            <button onClick={() => setRejectTarget(null)} style={styles.cancelBtn}>Отказ</button>
            <button onClick={() => { void handleReject(); }} style={styles.confirmRejectBtn} disabled={isRejecting}>
              {isRejecting ? 'Отхвърляне…' : 'Потвърди отхвърляне'}
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}

// ─── Proposal card ─────────────────────────────────────────────────────────────

function ProposalCard({
  proposal,
  onApprove,
  onReject,
}: {
  proposal: AdminPanelProposalResponse;
  onApprove: () => void;
  onReject: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const ai = proposal.aiReview;
  const isPending = proposal.proposalStatus === 'Pending' || proposal.proposalStatus === 'None';
  const isApproved = proposal.proposalStatus === 'Approved';
  const isRejected = proposal.proposalStatus === 'Rejected';

  return (
    <div style={styles.card}>
      {/* Card header */}
      <div style={styles.cardHeader}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <span style={styles.cardTitle}>{proposal.title}</span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 4, flexWrap: 'wrap' }}>
            <StatusBadge status={proposal.proposalStatus} />
            {ai && <ScoreBadge score={ai.reliabilityScore} />}
            <span style={styles.cardDate}>
              {new Date(proposal.createdAtUtc).toLocaleDateString('bg-BG', {
                day: '2-digit', month: 'short', year: 'numeric',
              })}
            </span>
          </div>
        </div>
        <button onClick={() => setExpanded((v) => !v)} style={styles.expandBtn}>
          <i className={`fas fa-chevron-${expanded ? 'up' : 'down'}`} />
        </button>
      </div>

      {/* AI summary */}
      {ai && (
        <div style={styles.aiSummary}>
          <i className="fas fa-robot" style={{ marginRight: 4, color: '#60a5fa' }} />
          {ai.summary}
          {ai.warnings.length > 0 && (
            <ul style={styles.warningList}>
              {ai.warnings.map((w, i) => (
                <li key={i} style={styles.warningItem}>
                  <i className="fas fa-triangle-exclamation" style={{ color: '#f59e0b', marginRight: 4 }} />
                  {w}
                </li>
              ))}
            </ul>
          )}
          {ai.suggestedName && (
            <div style={styles.aiSuggestion}>
              <b>Предложено:</b> {ai.suggestedName}
              {ai.suggestedLocation && ` · ${ai.suggestedLocation}`}
              {ai.suggestedRegion && ` · ${ai.suggestedRegion}`}
              {' · '}{ai.suggestedDifficultyLevel}
            </div>
          )}
        </div>
      )}

      {/* Rejection reason */}
      {isRejected && proposal.rejectionReason && (
        <div style={styles.rejectionNote}>
          <i className="fas fa-ban" style={{ marginRight: 4 }} /> {proposal.rejectionReason}
        </div>
      )}

      {/* Approved trail link */}
      {isApproved && proposal.trailId && (
        <div style={styles.approvedNote}>
          <i className="fas fa-circle-check" style={{ marginRight: 4 }} /> Trail #{proposal.trailId} — {proposal.trailName}
        </div>
      )}

      {/* Expanded content */}
      {expanded && (
        <div style={styles.cardBody}>
          <p style={styles.cardContent}>{proposal.content}</p>
          {proposal.imageUrls.length > 0 && (
            <div style={styles.imgGrid}>
              {proposal.imageUrls.map((url, i) => (
                <img key={i} src={url} alt="" style={styles.thumbnail} />
              ))}
            </div>
          )}
        </div>
      )}

      {/* Actions */}
      {isPending && (
        <div style={styles.cardActions}>
          <button onClick={onApprove} style={styles.approveBtn}>
            <i className="fas fa-circle-check" /> Одобри
          </button>
          <button onClick={onReject} style={styles.rejectBtn}>
            <i className="fas fa-circle-xmark" /> Отхвърли
          </button>
        </div>
      )}
    </div>
  );
}

// ─── Small reusable components ────────────────────────────────────────────────

function Modal({ children, onClose }: { children: React.ReactNode; onClose: () => void }) {
  return (
    <div style={styles.overlay} onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div style={styles.modalBox}>
        <button onClick={onClose} style={styles.closeModalBtn}>&times;</button>
        {children}
      </div>
    </div>
  );
}

function InputField({
  label, value, onChange, type = 'text',
}: {
  label: string; value: string; onChange: (v: string) => void; type?: string;
}) {
  return (
    <div>
      <label style={styles.label}>{label}</label>
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={styles.input}
        step={type === 'number' ? 'any' : undefined}
      />
    </div>
  );
}

function SelectField({
  label, value, onChange, options, labels,
}: {
  label: string; value: string; onChange: (v: string) => void; options: string[]; labels: string[];
}) {
  return (
    <div>
      <label style={styles.label}>{label}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)} style={styles.input}>
        {options.map((o, i) => <option key={o} value={o}>{labels[i]}</option>)}
      </select>
    </div>
  );
}

function CheckboxField({
  label, checked, onChange,
}: {
  label: string; checked: boolean; onChange: (v: boolean) => void;
}) {
  return (
    <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer', color: '#cbd5e1', fontSize: '0.88rem' }}>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        style={{ width: 16, height: 16, cursor: 'pointer' }}
      />
      {label}
    </label>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
  loginWrap: {
    minHeight: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    background: '#0f172a',
    padding: '20px',
  },
  loginCard: {
    background: '#1e293b',
    borderRadius: 16,
    padding: '40px 36px',
    width: '100%',
    maxWidth: 400,
    boxShadow: '0 20px 60px rgba(0,0,0,0.5)',
  },
  loginLogo: {
    textAlign: 'center',
    marginBottom: 12,
  },
  loginTitle: {
    margin: 0,
    fontSize: '1.4rem',
    fontWeight: 700,
    color: '#f1f5f9',
    textAlign: 'center',
  },
  loginSub: {
    margin: '4px 0 24px',
    color: '#64748b',
    fontSize: '0.85rem',
    textAlign: 'center',
  },
  loginForm: {
    display: 'flex',
    flexDirection: 'column',
    gap: 12,
  },
  loginLabel: {
    color: '#94a3b8',
    fontSize: '0.83rem',
    fontWeight: 600,
    marginBottom: 2,
  },
  loginInput: {
    background: '#0f172a',
    border: '1px solid #334155',
    borderRadius: 8,
    padding: '10px 14px',
    color: '#f1f5f9',
    fontSize: '0.95rem',
    outline: 'none',
    width: '100%',
    boxSizing: 'border-box',
  },
  loginBtn: {
    marginTop: 8,
    background: '#22c55e',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '12px',
    fontWeight: 700,
    fontSize: '1rem',
    cursor: 'pointer',
  },
  panelWrap: {
    minHeight: '100vh',
    background: '#0f172a',
    color: '#f1f5f9',
    display: 'flex',
    flexDirection: 'column',
  },
  header: {
    background: '#1e293b',
    padding: '14px 24px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderBottom: '1px solid #334155',
    position: 'sticky',
    top: 0,
    zIndex: 10,
  },
  logoutBtn: {
    background: 'transparent',
    color: '#94a3b8',
    border: '1px solid #334155',
    borderRadius: 8,
    padding: '6px 14px',
    cursor: 'pointer',
    fontSize: '0.85rem',
    display: 'flex',
    alignItems: 'center',
    gap: 6,
  },
  filterBar: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    padding: '14px 24px',
    borderBottom: '1px solid #1e293b',
    flexWrap: 'wrap',
  },
  filterBtn: {
    border: '1px solid #334155',
    borderRadius: 99,
    padding: '5px 16px',
    cursor: 'pointer',
    fontSize: '0.82rem',
    fontWeight: 600,
    transition: 'background 0.15s',
  },
  reloadBtn: {
    background: 'transparent',
    border: '1px solid #334155',
    borderRadius: 8,
    padding: '5px 10px',
    color: '#94a3b8',
    cursor: 'pointer',
    marginLeft: 4,
  },
  content: {
    flex: 1,
    padding: '20px 24px',
    maxWidth: 960,
    margin: '0 auto',
    width: '100%',
    boxSizing: 'border-box',
  },
  loadingMsg: {
    color: '#94a3b8',
    padding: '20px 0',
    textAlign: 'center',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  emptyMsg: {
    color: '#64748b',
    textAlign: 'center',
    padding: '40px 0',
    fontSize: '1rem',
  },
  proposalGrid: {
    display: 'flex',
    flexDirection: 'column',
    gap: 14,
  },
  card: {
    background: '#1e293b',
    borderRadius: 12,
    border: '1px solid #334155',
    overflow: 'hidden',
  },
  cardHeader: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: 12,
    padding: '14px 16px 8px',
  },
  cardTitle: {
    fontSize: '0.98rem',
    fontWeight: 700,
    color: '#f1f5f9',
    display: 'block',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  cardDate: {
    fontSize: '0.78rem',
    color: '#64748b',
  },
  expandBtn: {
    background: 'transparent',
    border: 'none',
    color: '#94a3b8',
    cursor: 'pointer',
    padding: '4px 6px',
    flexShrink: 0,
  },
  aiSummary: {
    background: '#0f172a',
    margin: '0 16px 8px',
    borderRadius: 8,
    padding: '10px 12px',
    fontSize: '0.83rem',
    color: '#94a3b8',
    borderLeft: '3px solid #60a5fa',
  },
  warningList: {
    margin: '6px 0 0',
    padding: '0 0 0 4px',
    listStyle: 'none',
  },
  warningItem: {
    fontSize: '0.8rem',
    color: '#fbbf24',
    marginTop: 3,
  },
  aiSuggestion: {
    marginTop: 6,
    fontSize: '0.8rem',
    color: '#7dd3fc',
  },
  rejectionNote: {
    margin: '0 16px 8px',
    padding: '8px 12px',
    background: '#450a0a',
    borderRadius: 8,
    color: '#fca5a5',
    fontSize: '0.83rem',
    borderLeft: '3px solid #ef4444',
  },
  approvedNote: {
    margin: '0 16px 8px',
    padding: '8px 12px',
    background: '#052e16',
    borderRadius: 8,
    color: '#86efac',
    fontSize: '0.83rem',
    borderLeft: '3px solid #22c55e',
  },
  cardBody: {
    padding: '0 16px 12px',
  },
  cardContent: {
    color: '#cbd5e1',
    fontSize: '0.87rem',
    lineHeight: 1.6,
    whiteSpace: 'pre-wrap',
    maxHeight: 200,
    overflow: 'auto',
    margin: '8px 0',
  },
  imgGrid: {
    display: 'flex',
    gap: 8,
    flexWrap: 'wrap',
    marginTop: 8,
  },
  thumbnail: {
    width: 80,
    height: 60,
    objectFit: 'cover',
    borderRadius: 6,
    border: '1px solid #334155',
  },
  cardActions: {
    display: 'flex',
    gap: 10,
    padding: '10px 16px 14px',
    borderTop: '1px solid #1e293b',
  },
  approveBtn: {
    background: '#15803d',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '7px 18px',
    cursor: 'pointer',
    fontWeight: 600,
    fontSize: '0.85rem',
    display: 'flex',
    alignItems: 'center',
    gap: 6,
  },
  rejectBtn: {
    background: '#7f1d1d',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '7px 18px',
    cursor: 'pointer',
    fontWeight: 600,
    fontSize: '0.85rem',
    display: 'flex',
    alignItems: 'center',
    gap: 6,
  },
  overlay: {
    position: 'fixed',
    inset: 0,
    background: 'rgba(0,0,0,0.7)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 100,
    padding: 20,
  },
  modalBox: {
    background: '#1e293b',
    borderRadius: 16,
    padding: '28px 28px 24px',
    width: '100%',
    maxWidth: 560,
    maxHeight: '85vh',
    overflowY: 'auto',
    position: 'relative',
    boxShadow: '0 20px 60px rgba(0,0,0,0.6)',
  },
  closeModalBtn: {
    position: 'absolute',
    top: 12,
    right: 16,
    background: 'transparent',
    border: 'none',
    color: '#64748b',
    fontSize: '1.4rem',
    cursor: 'pointer',
    lineHeight: 1,
  },
  modalTitle: {
    margin: '0 0 6px',
    fontSize: '1.1rem',
    fontWeight: 700,
    display: 'flex',
    alignItems: 'center',
    gap: 8,
  },
  modalSub: {
    color: '#94a3b8',
    fontSize: '0.85rem',
    margin: '0 0 16px',
  },
  formGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: 12,
    marginBottom: 12,
  },
  label: {
    display: 'block',
    color: '#94a3b8',
    fontSize: '0.78rem',
    fontWeight: 600,
    marginBottom: 4,
  },
  input: {
    width: '100%',
    background: '#0f172a',
    border: '1px solid #334155',
    borderRadius: 7,
    padding: '8px 10px',
    color: '#f1f5f9',
    fontSize: '0.88rem',
    outline: 'none',
    boxSizing: 'border-box',
  },
  modalActions: {
    display: 'flex',
    justifyContent: 'flex-end',
    gap: 10,
    marginTop: 20,
  },
  cancelBtn: {
    background: '#334155',
    color: '#cbd5e1',
    border: 'none',
    borderRadius: 8,
    padding: '9px 20px',
    cursor: 'pointer',
    fontWeight: 600,
    fontSize: '0.88rem',
  },
  confirmApproveBtn: {
    background: '#15803d',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '9px 20px',
    cursor: 'pointer',
    fontWeight: 700,
    fontSize: '0.88rem',
  },
  confirmRejectBtn: {
    background: '#b91c1c',
    color: '#fff',
    border: 'none',
    borderRadius: 8,
    padding: '9px 20px',
    cursor: 'pointer',
    fontWeight: 700,
    fontSize: '0.88rem',
  },
  errorBanner: {
    background: '#450a0a',
    color: '#fca5a5',
    border: '1px solid #7f1d1d',
    borderRadius: 8,
    padding: '8px 12px',
    fontSize: '0.85rem',
    marginTop: 8,
  },
};
