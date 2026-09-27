import React from 'react';

interface ConsoleCardProps {
  consoleRequest: string;
  consoleResponse: string;
}

export const ConsoleCard: React.FC<ConsoleCardProps> = ({ consoleRequest, consoleResponse }) => {
  return (
    <div className="glass-card" data-testid="console-card">
      <h2>
        <i className="fa-solid fa-terminal"></i> Execution Console
      </h2>
      <div className="payload-viewer">
        <div className="payload-block">
          <label>JSON-RPC Request</label>
          <pre className="code-block" id="jsonrpc-request" data-testid="console-request-output">
            {consoleRequest}
          </pre>
        </div>
        <div className="payload-block">
          <label>JSON-RPC Response</label>
          <pre className="code-block" id="jsonrpc-response" data-testid="console-response-output">
            {consoleResponse}
          </pre>
        </div>
      </div>
    </div>
  );
};
