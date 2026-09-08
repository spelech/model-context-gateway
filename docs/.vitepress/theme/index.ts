import DefaultTheme from 'vitepress/theme';
import type { Theme } from 'vitepress';
import './custom.css';
import { initMermaidPanZoom } from './mermaid-panzoom';

export default {
  extends: DefaultTheme,
  enhanceApp({ router }) {
    if (typeof window !== 'undefined') {
      initMermaidPanZoom();

      router.onAfterRouteChanged = () => {
        setTimeout(() => {
          initMermaidPanZoom();
        }, 150);
      };
    }
  }
} satisfies Theme;
