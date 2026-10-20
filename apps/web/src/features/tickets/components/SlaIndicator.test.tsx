import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const cases: { status: SlaStatus; expectedText: string; expectedGlyph: string }[] = [
    { status: 'WithinSla', expectedText: 'Within SLA', expectedGlyph: '●' },
    { status: 'AtRisk', expectedText: 'At risk', expectedGlyph: '⚠' },
    { status: 'Breached', expectedText: 'Breached', expectedGlyph: '✕' },
    { status: 'Met', expectedText: 'Met', expectedGlyph: '✓' },
    { status: 'NotApplicable', expectedText: 'No SLA', expectedGlyph: '—' },
  ];

  it.each(cases)(
    'renders $status with distinct text "$expectedText" and glyph "$expectedGlyph"',
    ({ status, expectedText, expectedGlyph }) => {
      const { container } = render(<SlaIndicator slaStatus={status} />);

      expect(screen.getByText(expectedText)).toBeInTheDocument();
      expect(screen.getByText(expectedGlyph)).toBeInTheDocument();

      const indicator = container.querySelector('[data-status]');
      expect(indicator).toHaveAttribute('data-status', status);
    },
  );

  it('renders due date when showDue is true and dueAtUtc is provided', () => {
    render(
      <SlaIndicator
        slaStatus="AtRisk"
        dueAtUtc="2026-10-19T14:30:00Z"
        showDue={true}
      />,
    );

    expect(screen.getByText('At risk')).toBeInTheDocument();
    // Due date should be present in the container
    expect(screen.getByText(/2026/)).toBeInTheDocument();
  });
});
