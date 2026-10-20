import { Badge, type BadgeTone } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';

interface SlaConfig {
  label: string;
  tone: BadgeTone;
  icon: string;
}

const configs: Record<SlaStatus, SlaConfig> = {
  WithinSla: { label: 'Within SLA', tone: 'info', icon: '●' },
  AtRisk: { label: 'At risk', tone: 'warning', icon: '⚠' },
  Breached: { label: 'Breached', tone: 'danger', icon: '✕' },
  Met: { label: 'Met', tone: 'success', icon: '✓' },
  NotApplicable: { label: 'No SLA', tone: 'neutral', icon: '—' },
};

export interface SlaIndicatorProps {
  slaStatus: SlaStatus;
  dueAtUtc?: string | null;
  showDue?: boolean;
}

/**
 * Reusable SLA indicator conveying status via tone, text, and distinct glyphs
 * so that state is never communicated by colour alone.
 */
export function SlaIndicator({ slaStatus, dueAtUtc, showDue = false }: SlaIndicatorProps) {
  const config = configs[slaStatus] ?? {
    label: slaStatus,
    tone: 'neutral' as BadgeTone,
    icon: '•',
  };

  return (
    <div className="sla-cell" data-testid="sla-indicator" data-status={slaStatus}>
      <Badge tone={config.tone}>
        <span className="sla-indicator">
          <span className="sla-indicator__icon" aria-hidden="true">
            {config.icon}
          </span>
          <span className="sla-indicator__label">{config.label}</span>
        </span>
      </Badge>
      {showDue && dueAtUtc && (
        <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
      )}
    </div>
  );
}
