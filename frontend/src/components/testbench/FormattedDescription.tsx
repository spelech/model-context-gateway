import React from 'react';

import { cleanDescription, renderInline, parseBlocks } from './descriptionUtils';

interface FormattedDescriptionProps {
  text?: string;
  serverId?: string;
  className?: string;
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
