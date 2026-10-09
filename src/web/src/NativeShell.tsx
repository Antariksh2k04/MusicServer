import { IonApp, IonContent, IonHeader, IonPage, IonTitle, IonToolbar } from '@ionic/react';

export default function NativeShell() {
  return (
    <IonApp>
      <IonPage>
        <IonHeader><IonToolbar><IonTitle>Music Server Shell</IonTitle></IonToolbar></IonHeader>
        <IonContent>
          <main>
            <h1>Android installation check</h1>
            <p className="notice">The Ionic Android shell is running.</p>
            <p>This development build checks installation and launch. Sign-in, uploads, streaming,
              and background playback will be added after this check.</p>
          </main>
        </IonContent>
      </IonPage>
    </IonApp>
  );
}
