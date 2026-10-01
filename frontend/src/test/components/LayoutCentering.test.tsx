import { describe, it, expect, beforeEach } from 'vitest';
import { render, act } from '@testing-library/react';
import fs from 'fs';
import path from 'path';
import App from '../../App';
import { SettingsView } from '../../components/settings/SettingsView';
import { AppKeysCard } from '../../components/clients/AppKeysCard';
import { useUserStore } from '../../stores/useUserStore';
import { mockApiResponse, defaultMockData } from '../setup';

describe('Layout and Sub-Navigation Centering', () => {
  beforeEach(() => {
    mockApiResponse('/api/me', defaultMockData.me);
    useUserStore.setState({
      user: defaultMockData.me
    });
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Navigation tab bars use centered alignment across top bar and sub bars.
   */
  it('renders top navigation bar with centered alignment in layout.css and App', async () => {
    await act(async () => {
      render(<App />);
    });

    const nav = document.querySelector('nav.tabs-nav');
    expect(nav).toBeInTheDocument();

    const cssPath = path.resolve(__dirname, '../../styles/layout.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');
    
    // Check that .tabs-nav in layout.css specifies justify-content: center
    const tabsNavMatch = cssContent.match(/\.tabs-nav\s*\{([^}]+)\}/);
    expect(tabsNavMatch).not.toBeNull();
    expect(tabsNavMatch![1]).toContain('justify-content: center');
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Navigation tab bars use centered alignment across top bar and sub bars.
   */
  it('renders tester tabs with centered alignment in tester.css', () => {
    const cssPath = path.resolve(__dirname, '../../styles/tester.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    // Check that .tester-tabs in tester.css specifies justify-content: center
    const testerTabsMatch = cssContent.match(/\.tester-tabs\s*\{([^}]+)\}/);
    expect(testerTabsMatch).not.toBeNull();
    expect(testerTabsMatch![1]).toContain('justify-content: center');
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Navigation tab bars use centered alignment across top bar and sub bars.
   */
  it('renders SettingsView sub-navigation bar with centered alignment', async () => {
    await act(async () => {
      render(<SettingsView />);
    });

    const subNav = document.querySelector('.settings-sub-nav') as HTMLElement;
    expect(subNav).toBeInTheDocument();
    expect(subNav.style.justifyContent).toBe('center');
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Navigation tab bars use centered alignment across top bar and sub bars.
   */
  it('renders AppKeysCard sub-navigation tabs with centered alignment for admin', async () => {
    await act(async () => {
      render(<AppKeysCard />);
    });

    const subNav = document.querySelector('.sub-tabs-nav') as HTMLElement;
    expect(subNav).toBeInTheDocument();
    expect(subNav.style.justifyContent).toBe('center');
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Ambient background glowing decorations use body pseudo-elements instead of DOM nodes.
   */
  it('uses body::before and body::after pseudo-elements for ambient gradients and removes background-decor DOM nodes', async () => {
    await act(async () => {
      render(<App />);
    });

    // Ensure no .background-decor DOM nodes exist in App
    const bgDecor = document.querySelector('.background-decor');
    expect(bgDecor).toBeNull();

    const cssPath = path.resolve(__dirname, '../../styles/layout.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    expect(cssContent).toContain('body::before');
    expect(cssContent).toContain('body::after');
    expect(cssContent).toContain('pointer-events: none;');
    expect(cssContent).toContain('position: fixed;');
    expect(cssContent).toContain('filter: blur(120px);');
    expect(cssContent).toContain('contain: paint;');
    expect(cssContent).toContain('z-index: -1;');
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Focus-visible outline styles are defined for keyboard accessibility.
   */
  it('defines focus-visible outline indicators for interactive focus styling', () => {
    const cssPath = path.resolve(__dirname, '../../styles/components.css');
    const cssContent = fs.readFileSync(cssPath, 'utf-8');

    expect(cssContent).toContain(':focus-visible');
    expect(cssContent).toContain('outline: 2px solid var(--primary);');
    expect(cssContent).toContain('outline-offset: 2px;');
  });

  /**
   * @requirement UI-140
   * @category UI
   * @type PositiveFeature
   * @description Navigation tabs maintain static 1px baseline border to eliminate vertical height jumps across active toggles
   */
  it('defines baseline 1px transparent border and box-sizing on navigation tab buttons to prevent layout shifts', () => {
    const layoutCss = fs.readFileSync(path.resolve(__dirname, '../../styles/layout.css'), 'utf-8');
    const tabBtnMatch = layoutCss.match(/\.tab-btn\s*\{([^}]+)\}/);
    expect(tabBtnMatch).not.toBeNull();
    expect(tabBtnMatch![1]).toContain('border: 1px solid transparent;');
    expect(tabBtnMatch![1]).toContain('box-sizing: border-box;');

    const testerCss = fs.readFileSync(path.resolve(__dirname, '../../styles/tester.css'), 'utf-8');
    const testerTabBtnMatch = testerCss.match(/\.tester-tab-btn\s*\{([^}]+)\}/);
    expect(testerTabBtnMatch).not.toBeNull();
    expect(testerTabBtnMatch![1]).toContain('border: 1px solid transparent;');
    expect(testerTabBtnMatch![1]).toContain('box-sizing: border-box;');

    expect(testerCss).toContain('.tester-sticky-console');
    expect(testerCss).toContain('position: sticky;');
  });
});

