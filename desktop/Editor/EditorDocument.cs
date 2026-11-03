namespace KitaplikEditoru.Editor;

public static class EditorDocument
{
    public const string Html = """
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta http-equiv="X-UA-Compatible" content="IE=edge">
          <base id="contentBase" href="about:blank">
          <style>
            :root { color-scheme: light; font-family: Georgia, 'Times New Roman', serif; color: #242a31; background: #e8ebef; }
            * { box-sizing: border-box; }
            body { margin: 0; min-height: 100vh; padding: 32px 16px; }
            #page { width: min(100%, 820px); min-height: calc(100vh - 64px); margin: 0 auto; padding: 72px 82px; background: #fff; box-shadow: 0 2px 18px #17202b1c; line-height: 1.7; font-size: 16px; outline: none; overflow-wrap: anywhere; }
            #page:empty::before { content: __START_WRITING__; color: #a0a8b2; pointer-events: none; }
            a { color: #176b9b; text-decoration: underline; }
            img { max-width: 100%; height: auto; }
            table { border-collapse: collapse; max-width: 100%; }
            td, th { border: 1px solid #8a929d; padding: 5px 8px; }
            .invisible-space::after { content: '·'; color: #8e98a4; }
            .invisible-tab::after { content: '→'; color: #8e98a4; }
            .invisible-newline::after { content: '↵'; color: #8e98a4; }
            html[data-theme='dark'] { color-scheme: dark; color: #e5e9ed; background: #171b1f; }
            html[data-theme='dark'] body { background: #171b1f; }
            html[data-theme='dark'] #page { color: #e5e9ed; background: #252b31; box-shadow: 0 2px 18px #0008; }
            html[data-theme='dark'] a { color: #8bc8f0; }
            @media (max-width: 700px) { body { padding: 0; } #page { min-height: 100vh; padding: 32px 24px; } }
          </style>
        </head>
        <body>
          <article id="page" contenteditable="true" spellcheck="true" lang="tr"></article>
          <script>
            const page = document.getElementById('page');
            const voidTags = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'param', 'source', 'track', 'wbr']);
            function escapeXml(value) { return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;').replace(/'/g, '&apos;'); }
            function serialize(node) {
              if (node.nodeType === Node.TEXT_NODE) return escapeXml(node.nodeValue || '');
              if (node.nodeType === Node.COMMENT_NODE) return '<!--' + (node.nodeValue || '').replace(/--/g, '- -') + '-->';
              if (node.nodeType !== Node.ELEMENT_NODE) return '';
              if (node.dataset.invisibleChar) return escapeXml(node.dataset.invisibleChar);
              const tag = node.localName.toLowerCase();
              const namespaceUris = { epub: 'http://www.idpf.org/2007/ops', xlink: 'http://www.w3.org/1999/xlink' };
              const namespaceAttrs = Object.entries(namespaceUris)
                .filter(([prefix]) => Array.from(node.attributes).some(attr => attr.name.toLowerCase().startsWith(prefix + ':')) && !node.hasAttribute('xmlns:' + prefix))
                .map(([prefix, uri]) => ' xmlns:' + prefix + '=\"' + uri + '\"').join('');
              const attrs = Array.from(node.attributes).map(attr => ' ' + attr.name.toLowerCase() + '=\"' + escapeXml(attr.value) + '\"').join('');
              if (voidTags.has(tag)) return '<' + tag + namespaceAttrs + attrs + ' />';
              return '<' + tag + namespaceAttrs + attrs + '>' + Array.from(node.childNodes).map(serialize).join('') + '</' + tag + '>';
            }
            function serializeContent() { return Array.from(page.childNodes).map(serialize).join(''); }
            let currentSectionId = null;
            let inputTimer;
            let inspectorTimer;
            function flushContent() {
              clearTimeout(inputTimer);
              if (currentSectionId) chrome.webview.postMessage(JSON.stringify({ type: 'content', sectionId: currentSectionId, content: serializeContent() }));
            }
            function setContent(markup, sectionId) {
              currentSectionId = sectionId;
              page.innerHTML = markup;
              page.scrollTop = 0;
              if (invisibleCharacters) setInvisibleCharacters(true);
              sendInspector();
            }
            function setBasePath(path) { document.getElementById('contentBase').href = path; }
            function setTheme(dark) { document.documentElement.dataset.theme = dark ? 'dark' : 'light'; }
            function setStyles(paths) {
              document.querySelectorAll('link[data-book-style]').forEach(link => link.remove());
              for (const path of paths) {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = path;
                link.dataset.bookStyle = 'true';
                document.head.appendChild(link);
              }
            }
            function undoEditorContent() {
              const before = serializeContent();
              page.focus();
              document.execCommand('undo');
              return before !== serializeContent();
            }
            function redoEditorContent() {
              const before = serializeContent();
              page.focus();
              document.execCommand('redo');
              return before !== serializeContent();
            }
            function formatText(command, value) { page.focus(); document.execCommand(command, false, value || null); }
            function pasteText(text, mode, isHtml) {
              page.focus();
              if (!isHtml || mode === 'plain') {
                if (isHtml) {
                  const source = document.createElement('div');
                  source.innerHTML = text;
                  source.querySelectorAll('script,style,meta,link,base,iframe,object,embed,form').forEach(element => element.remove());
                  text = source.textContent || '';
                }
                const plain = document.createElement('div');
                plain.textContent = text;
                document.execCommand('insertHTML', false, plain.innerHTML.replace(/\r\n|\n|\r/g, '<br />'));
                return;
              }
              const holder = document.createElement('div');
              holder.innerHTML = text;
              holder.querySelectorAll('script,style,meta,link,base,iframe,object,embed,form').forEach(element => element.remove());
              holder.querySelectorAll('*').forEach(element => {
                for (const attr of Array.from(element.attributes)) {
                  const name = attr.name.toLowerCase();
                  const value = attr.value.trim().replace(/[\u0000-\u0020]+/g, '').toLowerCase();
                  if (name.startsWith('on') || name === 'srcdoc' ||
                      ((name === 'href' || name === 'xlink:href' || name === 'src') && /^(javascript|vbscript|data):/.test(value)) ||
                      (mode !== 'preserve' && !['href', 'src', 'colspan', 'rowspan'].includes(name))) {
                    element.removeAttribute(attr.name);
                  }
                }
              });
              document.execCommand('insertHTML', false, holder.innerHTML.replace(/\u00a0/g, ' '));
            }
            function setBlock(tag) { page.focus(); document.execCommand('formatBlock', false, tag); }
            function applyStyle(className, isCharacter) {
              page.focus();
              const selection = window.getSelection();
              if (!selection || selection.rangeCount === 0) return;
              if (isCharacter) {
                if (selection.isCollapsed) return;
                const span = document.createElement('span');
                span.className = className;
                try { selection.getRangeAt(0).surroundContents(span); }
                catch (_) { span.appendChild(selection.getRangeAt(0).extractContents()); selection.getRangeAt(0).insertNode(span); }
              } else {
                let element = selection.anchorNode?.nodeType === Node.ELEMENT_NODE
                  ? selection.anchorNode : selection.anchorNode?.parentElement;
                element = element?.closest('p,h1,h2,h3,h4,h5,h6,blockquote,li') || page;
                if (element === page) { document.execCommand('formatBlock', false, 'p'); element = selection.anchorNode?.parentElement?.closest('p') || page; }
                if (element !== page) element.classList.add(className);
              }
              page.dispatchEvent(new Event('input', { bubbles: true }));
              sendInspector();
            }
            function getSelectedElement() {
              const selection = window.getSelection();
              const node = selection?.anchorNode;
              return (node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement)?.closest('#page *') || page;
            }
            function sendInspector() {
              clearTimeout(inspectorTimer);
              inspectorTimer = setTimeout(() => {
                const element = getSelectedElement();
                if (currentSectionId) chrome.webview.postMessage(JSON.stringify({
                  type: 'inspector', tag: element.localName, className: element.getAttribute('class') || '',
                  id: element.getAttribute('id') || '', style: element.getAttribute('style') || ''
                }));
              }, 80);
            }
            function applyInspector(tag, className, id, style) {
              let element = getSelectedElement();
              const allowed = new Set(['p','h1','h2','h3','h4','h5','h6','blockquote','div','span','strong','em','a','li']);
              if (allowed.has(tag) && element !== page && element.localName !== tag) {
                const replacement = document.createElement(tag);
                while (element.firstChild) replacement.appendChild(element.firstChild);
                for (const attribute of Array.from(element.attributes)) replacement.setAttribute(attribute.name, attribute.value);
                element.replaceWith(replacement);
                element = replacement;
              }
              if (element === page) return;
              className ? element.setAttribute('class', className) : element.removeAttribute('class');
              id ? element.setAttribute('id', id) : element.removeAttribute('id');
              style ? element.setAttribute('style', style) : element.removeAttribute('style');
              page.dispatchEvent(new Event('input', { bubbles: true }));
              sendInspector();
            }
            function insertLink() {
              const value = prompt(__LINK_PROMPT__);
              if (!value) return;
              try {
                const url = new URL(value);
                if (!['https:', 'http:', 'mailto:'].includes(url.protocol)) return;
                formatText('createLink', url.href);
              } catch (_) { }
            }
            function insertImage(src) { page.focus(); formatText('insertImage', src); }
            function findText(term) { page.focus(); window.find(term, false, false, true, false, false, false); }
            function replaceText(oldText, newText) {
              const walker = document.createTreeWalker(page, NodeFilter.SHOW_TEXT);
              let node;
              while ((node = walker.nextNode())) {
                if ((node.nodeValue || '').includes(oldText)) node.nodeValue = node.nodeValue.replaceAll(oldText, newText || '');
              }
              page.dispatchEvent(new Event('input', { bubbles: true }));
            }
            let invisibleCharacters = false;
            function setInvisibleCharacters(enabled) {
              if (enabled === invisibleCharacters) return;
              invisibleCharacters = enabled;
              if (!enabled) {
                page.querySelectorAll('span[data-invisible-char]').forEach(span => span.replaceWith(document.createTextNode(span.dataset.invisibleChar)));
                return;
              }
              const walker = document.createTreeWalker(page, NodeFilter.SHOW_TEXT);
              const textNodes = [];
              while (walker.nextNode()) textNodes.push(walker.currentNode);
              for (const textNode of textNodes) {
                const fragment = document.createDocumentFragment();
                for (const character of textNode.nodeValue || '') {
                  if (character === ' ' || character === '\t' || character === '\n') {
                    const marker = document.createElement('span');
                    marker.dataset.invisibleChar = character;
                    marker.className = character === ' ' ? 'invisible-space' : character === '\t' ? 'invisible-tab' : 'invisible-newline';
                    marker.textContent = character;
                    fragment.appendChild(marker);
                  } else {
                    fragment.appendChild(document.createTextNode(character));
                  }
                }
                textNode.replaceWith(fragment);
              }
            }
            function insertBookmark(id) {
              const safeId = id.replace(/[^A-Za-z0-9_-]/g, '-');
              if (safeId) formatText('insertHTML', '<span id="' + safeId + '"></span>');
            }
            function insertInternalLink(id) {
              const safeId = id.replace(/[^A-Za-z0-9_-]/g, '-');
              if (safeId) formatText('createLink', '#' + safeId);
            }
            function insertPageBreak() {
              formatText('insertHTML', '<div style="page-break-before: always; break-before: page;"></div><p></p>');
            }
            function insertFootnote() {
              formatText('insertHTML', '<aside xmlns:epub="http://www.idpf.org/2007/ops" epub:type="footnote" id="footnote-' + Date.now() + '"><p></p></aside>');
            }
            function navigateToFragment(fragment) {
              const target = document.getElementById(fragment);
              if (target) { target.scrollIntoView({ behavior: 'smooth', block: 'center' }); }
            }
            function splitAtCaret() {
              const selection = window.getSelection();
              if (!selection || selection.rangeCount === 0 || !page.contains(selection.anchorNode)) return null;
              const range = selection.getRangeAt(0).cloneRange();
              range.setEnd(page, page.childNodes.length);
              const tail = range.extractContents();
              page.dispatchEvent(new Event('input', { bubbles: true }));
              return { before: serializeContent(), after: Array.from(tail.childNodes).map(serialize).join('') };
            }
            page.addEventListener('input', () => {
              clearTimeout(inputTimer);
              if (currentSectionId) chrome.webview.postMessage(JSON.stringify({ type: 'editing', sectionId: currentSectionId }));
              inputTimer = setTimeout(flushContent, 120);
            });
            document.addEventListener('selectionchange', sendInspector);
            page.addEventListener('paste', event => {
              event.preventDefault();
              const text = event.clipboardData?.getData('text/plain') || '';
              const holder = document.createElement('div');
              holder.textContent = text;
              formatText('insertHTML', holder.innerHTML.replace(/\r\n|\n|\r/g, '<br />'));
            });
            page.addEventListener('keydown', event => {
              if (event.ctrlKey && event.key.toLowerCase() === 's') {
                event.preventDefault();
                flushContent();
                chrome.webview.postMessage(JSON.stringify({ type: 'save' }));
              }
            });
          </script>
        </body>
        </html>
        """;
}