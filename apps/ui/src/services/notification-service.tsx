import React, { createContext, use, useEffect, type ReactNode } from 'react';
import * as signalR from '@microsoft/signalr';
import { EventEmitter } from 'eventemitter3';
import { getOidc } from '@/oidc';

export type MessageTarget = 'application' | 'environment' | 'deployment' | 'apiKey';

export interface Message {
  name: string;
  target: MessageTarget;
  timestamp: string;
}

export interface Events {
  onNotification: (message: Message) => void;
}

export interface NotificationContextType {
  subscribe: (callback: (message: Message) => void, to?: MessageTarget[]) => () => void;
}

const NotificationContext = createContext<NotificationContextType | undefined>(undefined);

interface NotificationProviderProps {
  children: ReactNode;
}

const emitter = new EventEmitter<Events>();
const baseUrl = `${window.location.protocol}//${window.location.host}${import.meta.env.VITE_API_BASE_URL || import.meta.env.BASE_URL || ''}`;

export const NotificationProvider: React.FC<NotificationProviderProps> = ({ children }) => {
  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}api/notifications`, {
        accessTokenFactory: async () => {
          const oidc = await getOidc();
          return await oidc.getAccessToken();
        }
      })
      .withAutomaticReconnect()
      .build();

    newConnection
      .start()
      .then(() => {
        console.log('Connected to SignalR hub');

        // Listen for server-side notifications
        newConnection.on('onNotification', (message: Message) => {
          console.log('SignalR Notification received: ', message);
          //const parsedMessage = JSON.parse(message) as Message;
          emitter.emit('onNotification', message);
        });
      })
      .catch((err) => console.error('SignalR Connection Error: ', err));

    return () => {
      newConnection.stop().catch((err) => console.error('Error stopping SignalR connection: ', err));
    };
  }, [baseUrl]);

  const subscribe = (callback: (message: Message) => void, to?: MessageTarget[]) => {
    const processor = (message: Message) => {
      if (to && to.filter((o) => o === message.target).length === 0) {
        return;
      }
      callback(message);
    };
    const unsubscribe = () => {
      emitter.off('onNotification', processor);
    };
    emitter.on('onNotification', processor);
    return unsubscribe;
  };

  return <NotificationContext value={{ subscribe: subscribe }}>{children}</NotificationContext>;
};

export const useNotifications = (): NotificationContextType => {
  const context = use(NotificationContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationProvider');
  }
  return context;
};
