import React from 'react';

interface FormattedDescriptionProps {
  text?: string;
  serverId?: string;
  className?: string;
}

/**
 * Strips any redundant leading server tag prefix like [docker] or [context7].
 */
export function cleanDescription(text?: string, serverId?: string): string {
  if (!text) return '';
  let cleaned = text.trim();

  if (serverId) {
    const escaped = serverId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    cleaned = cleaned.replace(new RegExp(`^\\[${escaped}\\]\\s*`, 'i'), '');
  }

  // Also remove generic leading bracketed server tag if present
  cleaned = cleaned.replace(/^\[[a-zA-Z0-9_\-.:/]+\]\s*/, '');
  return cleaned.trim();
}

/**
 * Renders inline markdown tokens: `code`, **bold**, *italic*, and [link](url).
 */
export function renderInline(text: string): React.ReactNode[] {
  const INLINE_REGEX = /(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*|\[[^\]]+\]\([^)]+\))/g;
  const parts = text.split(INLINE_REGEX);

  return parts.map((part, index) => {
    if (!part) return null;

    if (part.startsWith('`') && part.endsWith('`') && part.length >= 2) {
      return (
        <code key={index} className="desc-inline-code">
          {part.slice(1, -1)}
        </code>
      );
    }

    if (part.startsWith('**') && part.endsWith('**') && part.length >= 4) {
      return (
        <strong key={index} className="desc-bold">
          {part.slice(2, -2)}
        </strong>
      );
    }

    if (part.startsWith('*') && part.endsWith('*') && part.length >= 2) {
      return (
        <em key={index} className="desc-italic">
          {part.slice(1, -1)}
        </em>
      );
    }

    const linkMatch = part.match(/^\[(.*?)\]\((https?:\/\/[^\s)]+)\)$/);
    if (linkMatch) {
      return (
        <a
          key={index}
          href={linkMatch[2]}
          target="_blank"
          rel="noopener noreferrer"
          className="desc-link"
        >
          {linkMatch[1]}
        </a>
      );
    }

    return part;
  });
}

type BlockType = 'p' | 'ul' | 'ol';

interface Block {
  type: BlockType;
  lines: string[];
}

/**
 * Parses cleaned multiline text into structured paragraphs, unordered lists, and ordered lists.
 */
export function parseBlocks(text: string): Block[] {
  const rawLines = text.split(/\r?\n/);
  const blocks: Block[] = [];
  let currentBlock: Block | null = null;

  for (const line of rawLines) {
    const trimmed = line.trim();

    if (!trimmed) {
      // Empty line closes the current block
      if (currentBlock) {
        blocks.push(currentBlock);
        currentBlock = null;
      }
      continue;
    }

    const isUnordered = /^[-*]\s+/.test(trimmed);
    const isOrdered = /^\d+\.\s+/.test(trimmed);

    if (isUnordered) {
      const content = trimmed.replace(/^[-*]\s+/, '');
      if (currentBlock && currentBlock.type === 'ul') {
        currentBlock.lines.push(content);
      } else {
        if (currentBlock) blocks.push(currentBlock);
        currentBlock = { type: 'ul', lines: [content] };
      }
    } else if (isOrdered) {
      const content = trimmed.replace(/^\d+\.\s+/, '');
      if (currentBlock && currentBlock.type === 'ol') {
        currentBlock.lines.push(content);
      } else {
        if (currentBlock) blocks.push(currentBlock);
        currentBlock = { type: 'ol', lines: [content] };
      }
    } else {
      if (currentBlock && currentBlock.type === 'p') {
        currentBlock.lines.push(trimmed);
      } else {
        if (currentBlock) blocks.push(currentBlock);
        currentBlock = { type: 'p', lines: [trimmed] };
      }
    }
  }

  if (currentBlock) {
    blocks.push(currentBlock);
  }

  return blocks;
}

export const FormattedDescription: React.FC<FormattedDescriptionProps> = ({
  text,
  serverId,
  className,
}) => {
  const cleaned = cleanDescription(text, serverId);
  if (!cleaned) return null;

  const blocks = parseBlocks(cleaned);

  return (
    <div className={`tool-hint-desc formatted-description ${className || ''}`} data-testid="formatted-description">
      {blocks.map((block, bIdx) => {
        if (block.type === 'ul') {
          return (
            <ul key={bIdx} className="desc-ul">
              {block.lines.map((line, lIdx) => (
                <li key={lIdx}>{renderInline(line)}</li>
              ))}
            </ul>
          );
        }

        if (block.type === 'ol') {
          return (
            <ol key={bIdx} className="desc-ol">
              {block.lines.map((line, lIdx) => (
                <li key={lIdx}>{renderInline(line)}</li>
              ))}
            </ol>
          );
        }

        // Paragraph: join lines with line breaks if multiline within same paragraph
        return (
          <p key={bIdx} className="desc-p">
            {block.lines.map((line, lIdx) => (
              <React.Fragment key={lIdx}>
                {lIdx > 0 && <br />}
                {renderInline(line)}
              </React.Fragment>
            ))}
          </p>
        );
      })}
    </div>
  );
};
