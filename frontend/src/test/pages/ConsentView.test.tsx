import { render, screen } from '@testing-library/react';
import { ConsentView } from '../../pages/ConsentView';
import { describe, it, expect, beforeEach } from 'vitest';

describe('ConsentView Component', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'location', {
      value: {
        search: '?client_id=123&client_name=Slack%20App',
        pathname: '/consent'
      },
      writable: true
    });
  });

  /**
   * @requirement AUTH-109
   * @category AUTH
   * @type PositiveFeature
   * @description ConsentView properly renders the client name from query string and builds correct form action with hidden inputs.
   */
  it('renders client name from query string and sets form action and hidden inputs', () => {
    const { container } = render(<ConsentView />);
    
    expect(screen.getByText(/Authorize Access/i)).toBeInTheDocument();
    expect(screen.getByText(/Slack App/i)).toBeInTheDocument();
    
    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/connect/authorize');
    
    const clientIdInput = container.querySelector('input[name="client_id"]') as HTMLInputElement;
    expect(clientIdInput).toBeInTheDocument();
    expect(clientIdInput?.value).toBe('123');
  });
});
