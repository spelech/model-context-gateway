import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FormattedDescription } from '../../components/testbench/FormattedDescription';
import { cleanDescription } from '../../components/testbench/descriptionUtils';

describe('FormattedDescription Component', () => {
  /**
   * @requirement UI-138
   * @category UI
   * @type PositiveFeature
   * @description cleans redundant server prefix from descriptions
   */
  it('cleans redundant server prefix from descriptions', () => {
    expect(cleanDescription('[docker] List all containers', 'docker')).toBe('List all containers');
    expect(cleanDescription('[DOCKER] Run a container', 'docker')).toBe('Run a container');
    expect(cleanDescription('[context7] Resolves a package', 'context7')).toBe('Resolves a package');
    expect(cleanDescription('[custom-server] Some tool')).toBe('Some tool');
    expect(cleanDescription('[mysql-homeassistant] [MySQL MCP Server [vundefined]] Run SQL queries against HA', 'mysql-homeassistant')).toBe('Run SQL queries against HA');
    expect(cleanDescription('Normal description without prefix', 'docker')).toBe('Normal description without prefix');
    expect(cleanDescription('')).toBe('');
    expect(cleanDescription(undefined)).toBe('');
  });

  /**
   * @requirement UI-138
   * @category UI
   * @type PositiveFeature
   * @description renders structured paragraphs and inline code/bold markdown formatting
   */
  it('renders structured paragraphs and inline code/bold markdown formatting', () => {
    const text = `[docker] Run an image in a new Docker container.
Preferred over \`create_container\` + \`start_container\`.

Note: This is **very important** and *required*.`;

    render(<FormattedDescription text={text} serverId="docker" />);

    const container = screen.getByTestId('formatted-description');
    expect(container).toBeInTheDocument();

    // Verify leading server tag was stripped
    expect(screen.queryByText(/\[docker\]/i)).not.toBeInTheDocument();

    // Verify inline code elements
    const codeElements = screen.getAllByRole('code');
    const codeTexts = codeElements.map((el) => el.textContent);
    expect(codeTexts).toContain('create_container');
    expect(codeTexts).toContain('start_container');

    // Verify bold formatting
    const boldEl = screen.getByText('very important');
    expect(boldEl.tagName.toLowerCase()).toBe('strong');

    // Verify italic formatting
    const italicEl = screen.getByText('required');
    expect(italicEl.tagName.toLowerCase()).toBe('em');
  });

  /**
   * @requirement UI-138
   * @category UI
   * @type PositiveFeature
   * @description renders unordered and ordered lists with inline formatting
   */
  it('renders unordered and ordered lists with inline formatting', () => {
    const text = `Overview of parameters:

- Item \`alpha\`: First option
- Item **beta**: Second option

Steps to execute:
1. First step
2. Second step`;

    render(<FormattedDescription text={text} />);

    // Verify list items
    const listItems = screen.getAllByRole('listitem');
    expect(listItems.length).toBe(4);

    expect(screen.getByText('alpha')).toBeInTheDocument();
    expect(screen.getByText('beta')).toBeInTheDocument();
    expect(screen.getByText(/First step/)).toBeInTheDocument();
    expect(screen.getByText(/Second step/)).toBeInTheDocument();
  });

  /**
   * @requirement UI-138
   * @category UI
   * @type PositiveFeature
   * @description renders safe external links
   */
  it('renders safe external links', () => {
    const text = 'For documentation see [Official Docs](https://modelcontextprotocol.io).';

    render(<FormattedDescription text={text} />);

    const link = screen.getByRole('link', { name: 'Official Docs' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', 'https://modelcontextprotocol.io');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  /**
   * @requirement UI-138
   * @category UI
   * @type FailClosedGuardrail
   * @description renders null when text is empty or blank
   */
  it('renders null when text is empty or blank', () => {
    const { container: emptyContainer } = render(<FormattedDescription text="" />);
    expect(emptyContainer.firstChild).toBeNull();

    const { container: whitespaceContainer } = render(<FormattedDescription text="   " />);
    expect(whitespaceContainer.firstChild).toBeNull();

    const { container: prefixOnlyContainer } = render(<FormattedDescription text="[docker]" serverId="docker" />);
    expect(prefixOnlyContainer.firstChild).toBeNull();
  });
});
