import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RedirectUrisModal } from '../../components/clients/RedirectUrisModal';

describe('RedirectUrisModal Component', () => {
  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  const sampleUris = [
    'https://app.example.com/callback',
    'http://localhost:3000/callback',
    'vscode://ms-toolsai.vscode-ai/callback',
  ];

  /**
   * @requirement UI-139
   * @category UI
   * @type PositiveFeature
   * @description renders client name, registered URIs count, and each individual URI
   */
  it('renders client name, registered URIs count, and each individual URI', () => {
    render(
      <RedirectUrisModal
        isOpen={true}
        onClose={vi.fn()}
        clientName="My Claude Extension"
        redirectUris={sampleUris}
      />
    );

    expect(screen.getByText(/Redirect URIs — My Claude Extension/i)).toBeInTheDocument();
    expect(screen.getByText('3 Registered URIs')).toBeInTheDocument();

    expect(screen.getByText('https://app.example.com/callback')).toBeInTheDocument();
    expect(screen.getByText('http://localhost:3000/callback')).toBeInTheDocument();
    expect(screen.getByText('vscode://ms-toolsai.vscode-ai/callback')).toBeInTheDocument();
  });

  /**
   * @requirement UI-139
   * @category UI
   * @type PositiveFeature
   * @description copies individual URI and all URIs to clipboard
   */
  it('copies individual URI and all URIs to clipboard', () => {
    render(
      <RedirectUrisModal
        isOpen={true}
        onClose={vi.fn()}
        clientName="My Extension"
        redirectUris={sampleUris}
      />
    );

    // Copy single URI
    const copyFirstUriBtn = screen.getByTestId('btn-copy-uri-0');
    fireEvent.click(copyFirstUriBtn);
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('https://app.example.com/callback');

    // Copy all URIs
    const copyAllBtn = screen.getByTestId('btn-copy-all-uris');
    fireEvent.click(copyAllBtn);
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(sampleUris.join('\n'));
  });

  /**
   * @requirement UI-139
   * @category UI
   * @type PositiveFeature
   * @description calls onClose when close button is clicked
   */
  it('calls onClose when close button or dismiss is clicked', () => {
    const closeSpy = vi.fn();
    render(
      <RedirectUrisModal
        isOpen={true}
        onClose={closeSpy}
        clientName="My Extension"
        redirectUris={sampleUris}
      />
    );

    const closeBtn = screen.getByTestId('btn-close-redirect-uris-modal');
    fireEvent.click(closeBtn);
    expect(closeSpy).toHaveBeenCalled();
  });

  /**
   * @requirement UI-139
   * @category UI
   * @type FailClosedGuardrail
   * @description renders nothing when isOpen is false
   */
  it('renders nothing when isOpen is false', () => {
    const { container } = render(
      <RedirectUrisModal
        isOpen={false}
        onClose={vi.fn()}
        clientName="My Extension"
        redirectUris={sampleUris}
      />
    );

    expect(container.firstChild).toBeNull();
  });
});
