<script setup lang="ts">
import { ref } from 'vue'

import ModalShell from './ModalShell.vue'

defineEmits<{ close: [] }>()

type Tab = 'rules' | 'mapping'

const tab = ref<Tab>('rules')

/**
 * 界面上所有词都被换成了办公黑话，光看界面猜不出在打什么牌，
 * 所以这里把两套说法摆在一起。左边是骷髅王的原始概念，右边是屏幕上的样子。
 */
const glossary: { real: string; here: string; note: string }[] = [
  { real: '一局游戏', here: '本季度考核', note: '打满设定的轮数就结束' },
  { real: '一轮 / 第 N 轮', here: '第 N 项议程', note: '第 N 轮每人拿 N 张牌' },
  { real: '一墩（trick）', here: '一批任务', note: '每人各出一张，比大小定归属' },
  { real: '叫牌', here: '群接龙 · 承接量', note: '预报自己这轮能拿几墩' },
  { real: '手牌', here: '待办任务', note: '底部快捷回复条里那排' },
  { real: '出牌', here: '发消息 / 认领任务', note: '灰掉的是当前不能出的' },
  { real: '得分', here: '绩效', note: '右侧成员列表里的数字' },
  { real: '黑桃（王牌花色）', here: '管理层', note: '压得过其他三个组' },
  { real: '红/黄/蓝三色', here: '前端组 / 后端组 / 产品组', note: '互相之间不分高低' },
  { real: '逃跑牌', here: '本项跳过', note: '必输，用来避开不想要的墩' },
  { real: '海盗', here: '外部顾问', note: '压过所有数字牌' },
  { real: '美人鱼', here: '法务合规', note: '压过数字牌，专克骷髅王' },
  { real: '骷髅王', here: 'CEO 直批', note: '压过海盗，但怕美人鱼' },
  { real: '狄格雷丝（Tigress）', here: '机动人力', note: '出的时候自己选当顾问还是当跳过' },
]
</script>

<template>
  <ModalShell title="新人指引" :width="560" @close="$emit('close')">
    <div class="tabs">
      <button class="tab" :class="{ on: tab === 'rules' }" @click="tab = 'rules'">怎么玩</button>
      <button class="tab" :class="{ on: tab === 'mapping' }" @click="tab = 'mapping'">
        黑话对照
      </button>
    </div>

    <div v-if="tab === 'rules'" class="doc">
      <p class="lead">
        这是桌游《骷髅王》(Skull King) 的线上版，界面被整体换成了办公用语。
        规则和原版一致，只是每个词都换了层皮。
      </p>

      <section>
        <h3>一句话原理</h3>
        <p>
          每轮开始前先预报「我这轮能吃下几批任务」，然后打牌。
          <strong>报几就要正好拿几</strong>，多了少了都扣分——难点全在这个「正好」上。
        </p>
      </section>

      <section>
        <h3>一轮的流程</h3>
        <ol>
          <li>发牌。第 1 轮每人 1 张，第 2 轮 2 张，以此类推。</li>
          <li>所有人同时报承接量，全员报完才一起揭晓，中途看不到别人报了多少。</li>
          <li>轮流出牌，每人出一张算一批任务，比出谁接下这批。</li>
          <li>手牌打完这轮结束，按报的数和实际拿到的数结算绩效。</li>
        </ol>
      </section>

      <section>
        <h3>出牌限制</h3>
        <p>
          第一个人出的数字牌决定了这批任务的归口组别。后面的人
          <strong>手上有同组的数字牌就必须跟</strong>，没有才能随便打。
          特殊牌（跳过、顾问、法务、CEO、机动人力）任何时候都能出，不受限制。
        </p>
        <p class="muted">不用记这条，界面上不能出的牌是灰的。</p>
      </section>

      <section>
        <h3>谁接下这批任务</h3>
        <p>从大到小是这么个压制关系：</p>
        <ul class="chain">
          <li><strong>法务合规</strong>（美人鱼）只在同时出现 CEO 时最大</li>
          <li><strong>CEO 直批</strong>（骷髅王）压过所有顾问</li>
          <li><strong>外部顾问</strong>（海盗）压过所有数字牌，多个顾问看谁先出</li>
          <li><strong>法务合规</strong>压过所有数字牌</li>
          <li><strong>管理层</strong>（王牌花色）的数字压过其他三个组</li>
          <li>同组比数字大小；全是跳过的话，由第一个出牌的人兜底</li>
        </ul>
        <p class="muted">
          三者互相咬：法务克 CEO，CEO 克顾问，顾问克法务。三张同时出现时法务赢。
        </p>
      </section>

      <section>
        <h3>怎么算绩效</h3>
        <table class="score">
          <tbody>
            <tr>
              <td>报了 N（N&gt;0）且正好拿到 N</td>
              <td class="plus">+20 × N，还能拿奖励分</td>
            </tr>
            <tr>
              <td>报了 N 但没拿准</td>
              <td class="minus">每差一批 −10，奖励分全没</td>
            </tr>
            <tr>
              <td>报 0 且真的一批没接</td>
              <td class="plus">+10 × 轮数</td>
            </tr>
            <tr>
              <td>报 0 却接到了</td>
              <td class="minus">−10 × 轮数</td>
            </tr>
          </tbody>
        </table>
        <p class="muted">
          所以越到后面的轮次，报 0 的收益和风险越大。
        </p>
      </section>

      <section>
        <h3>奖励分</h3>
        <p>只有报数完全命中的人才拿得到，没命中就一分不算：</p>
        <ul>
          <li>接下的那批里每张 14：普通组 +10，管理层 +20</li>
          <li>用 CEO 直批压掉顾问：每个顾问 +30</li>
          <li>用顾问压掉 CEO 直批：+30</li>
          <li>用法务合规驳回 CEO 直批：+50</li>
        </ul>
      </section>

      <section>
        <h3>牌都有些什么</h3>
        <p>
          一共 70 张：四个组各 1–14（56 张）、跳过 5 张、外部顾问 5 张、
          机动人力 1 张、法务合规 2 张、CEO 直批 1 张。
        </p>
        <p class="muted">
          2 到 8 人都能玩，人多则轮数少——牌就这么多，第 N 轮要发掉 N × 人数 张。
          8 个人最多打到第 8 项议程。
        </p>
      </section>

      <section>
        <h3>顺手的几个键</h3>
        <p>
          <kbd>Esc</kbd> 一键切到纯工作对话，再按一下切回来，牌局不受影响。
          窗口失焦一段时间也会自动切过去，这些都能在「设置」里改。
        </p>
      </section>
    </div>

    <table v-else class="mapping">
      <thead>
        <tr>
          <th>原版说法</th>
          <th>这里显示成</th>
          <th>说明</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in glossary" :key="row.real">
          <td>{{ row.real }}</td>
          <td class="here">{{ row.here }}</td>
          <td class="muted">{{ row.note }}</td>
        </tr>
      </tbody>
    </table>
  </ModalShell>
</template>

<style scoped>
.tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 14px;
  border-bottom: 1px solid var(--line);
}

.tab {
  padding: 7px 14px;
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--text-secondary);
}

.tab.on {
  border-bottom-color: var(--accent);
  color: var(--accent);
}

.doc {
  font-size: 13px;
  line-height: 1.7;
}

.lead {
  margin: 0 0 14px;
  padding: 9px 12px;
  border-radius: var(--radius);
  background: var(--bg-hover);
  color: var(--text-secondary);
}

section {
  margin-bottom: 16px;
}

h3 {
  margin: 0 0 5px;
  font-size: 13px;
  font-weight: 600;
}

.doc p {
  margin: 0 0 6px;
}

.doc ol,
.doc ul {
  margin: 0;
  padding-left: 20px;
}

.doc li {
  margin-bottom: 2px;
}

.chain li {
  list-style: none;
  position: relative;
}

.chain li::before {
  content: '▸';
  position: absolute;
  left: -14px;
  color: var(--text-muted);
}

.score {
  width: 100%;
  border-collapse: collapse;
}

.score td {
  padding: 4px 0;
  border-bottom: 1px solid var(--line);
  vertical-align: top;
}

.score td:last-child {
  width: 42%;
  text-align: right;
  white-space: nowrap;
}

.plus {
  color: var(--success);
}

.minus {
  color: var(--danger);
}

.mapping {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.mapping th {
  padding: 5px 8px;
  border-bottom: 1px solid var(--line);
  color: var(--text-muted);
  font-weight: 500;
  text-align: left;
}

.mapping td {
  padding: 5px 8px;
  border-bottom: 1px solid var(--line);
  vertical-align: top;
}

.mapping .here {
  color: var(--accent);
}

.mapping .muted {
  font-size: 12px;
}

@media (max-width: 800px) {
  .mapping {
    display: block;
    overflow-x: auto;
  }

  .score td:last-child {
    width: auto;
    white-space: normal;
  }
}

kbd {
  padding: 1px 5px;
  border: 1px solid var(--line-strong);
  border-radius: 3px;
  background: var(--bg-panel);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 11px;
}
</style>
